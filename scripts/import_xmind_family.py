#!/usr/bin/env python3
import json
import re
import sqlite3
import zipfile
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path
from typing import Optional

DB_PATH = Path("/Users/admin/RiderProjects/FamilyTree/familytree.db")
XMIND_PATH = Path("/Users/admin/Library/Containers/com.tencent.xinWeChat/Data/Documents/xwechat_files/qq_30342221_9bfc/msg/file/2026-02/阳峪村董氏家谱4(1).xmind")

CN_DEPTH = {1: "一", 2: "二", 3: "三", 4: "四", 5: "五", 6: "六", 7: "七", 8: "八", 9: "九", 10: "十"}


def normalize(text: str) -> str:
    return " ".join((text or "").replace("\u200b", "").split())


def parse_birth_date(text: str) -> Optional[str]:
    m = re.search(r"((?:19|20)\d{2})[\.\-/年](\d{1,2})[\.\-/月](\d{1,2})", text)
    if not m:
        return None
    y, mo, d = map(int, m.groups())
    try:
        return datetime(y, mo, d).strftime("%Y-%m-%d")
    except ValueError:
        return None


def pick_generation(text: str, depth: int) -> Optional[str]:
    m = re.search(r"([一二三四五六七八九十]{1,2})\s*[_-]", text)
    if m:
        return m.group(1)
    return CN_DEPTH.get(min(depth + 1, 10))


def cleanup_name(name: str) -> str:
    return re.sub(r"\s+", "", name.strip())


def valid_name(name: str) -> bool:
    if not name:
        return False
    bad = ["xx", "XX", "...", "无法考证", "某", "可能"]
    return not any(b in name for b in bad)


def guess_gender(text: str, default: str = "Male") -> str:
    if text.startswith("妻") or "女：" in text or "女:" in text or "长女" in text or "次女" in text or "小女" in text:
        return "Female"
    if text.startswith("夫") or "婿" in text:
        return "Male"
    return default


@dataclass
class NodePerson:
    temp_id: int
    parent_temp_id: Optional[int]
    name: str
    gender: str
    generation: Optional[str]
    birth_date: Optional[str]
    is_root: bool
    spouse_name: Optional[str]


def extract_person(title: str, depth: int, is_root: bool) -> Optional[NodePerson]:
    txt = normalize(title)
    if not txt:
        return None

    names = re.findall(r"董[\u4e00-\u9fa5]{1,5}", txt)
    primary = names[0] if names else None

    # Spouse-only nodes can appear without a main Dong-surname name.
    if not primary and txt.startswith("妻"):
        m = re.search(r"妻[:：]\s*([\u4e00-\u9fa5]{2,6})", txt)
        if m:
            primary = cleanup_name(m.group(1))

    if not primary or not valid_name(primary):
        return None

    spouse_name = None
    m_wife = re.search(r"妻[:：]\s*([\u4e00-\u9fa5\s]{2,8})", txt)
    if m_wife:
        spouse_name = cleanup_name(m_wife.group(1))
    m_husband = re.search(r"夫[:：]\s*([\u4e00-\u9fa5\s]{2,8})", txt)
    if m_husband:
        spouse_name = cleanup_name(m_husband.group(1))

    if spouse_name and not valid_name(spouse_name):
        spouse_name = None

    gender = guess_gender(txt, "Male")
    generation = pick_generation(txt, depth)
    birth_date = parse_birth_date(txt)

    return NodePerson(
        temp_id=-1,
        parent_temp_id=None,
        name=primary,
        gender=gender,
        generation=generation,
        birth_date=birth_date,
        is_root=is_root,
        spouse_name=spouse_name,
    )


def load_xmind_people(xmind_path: Path) -> list[NodePerson]:
    with zipfile.ZipFile(xmind_path) as zf:
        content = json.loads(zf.read("content.json"))
    root = content[0]["rootTopic"]

    people: list[NodePerson] = []
    temp_counter = 0

    def walk(node: dict, depth: int, parent_person_temp_id: Optional[int]) -> None:
        nonlocal temp_counter
        title = node.get("title", "")
        info = extract_person(title, depth, is_root=(depth == 0))

        current_parent = parent_person_temp_id
        if info:
            temp_counter += 1
            info.temp_id = temp_counter
            info.parent_temp_id = parent_person_temp_id
            people.append(info)
            current_parent = info.temp_id

        for child in node.get("children", {}).get("attached", []):
            walk(child, depth + 1, current_parent)

    walk(root, 0, None)
    return people


def import_people(db_path: Path, people: list[NodePerson]) -> None:
    conn = sqlite3.connect(db_path)
    conn.row_factory = sqlite3.Row
    cur = conn.cursor()

    now = datetime.utcnow().strftime("%Y-%m-%d %H:%M:%S")

    cur.execute("PRAGMA foreign_keys=OFF;")
    cur.execute("DELETE FROM ShareLinks;")
    cur.execute("DELETE FROM Persons;")
    cur.execute("DELETE FROM sqlite_sequence WHERE name IN ('Persons','ShareLinks');")

    temp_to_db: dict[int, int] = {}

    for p in people:
        parent_db_id = temp_to_db.get(p.parent_temp_id) if p.parent_temp_id else None
        gender_num = 0 if p.gender == "Male" else 1
        cur.execute(
            """
            INSERT INTO Persons
            (Name, Gender, Generation, BirthDate, ParentId, SpouseId, IsRoot, CreatedAt, UpdatedAt)
            VALUES (?, ?, ?, ?, ?, NULL, ?, ?, ?)
            """,
            (
                p.name,
                gender_num,
                p.generation,
                p.birth_date,
                parent_db_id,
                1 if p.is_root else 0,
                now,
                now,
            ),
        )
        temp_to_db[p.temp_id] = cur.lastrowid

    # Link/create spouses after base rows exist.
    name_to_ids: dict[str, list[int]] = {}
    for row in cur.execute("SELECT Id, Name FROM Persons"):
        name_to_ids.setdefault(row["Name"], []).append(row["Id"])

    for p in people:
        if not p.spouse_name:
            continue

        person_id = temp_to_db[p.temp_id]
        cur.execute("SELECT SpouseId, Gender FROM Persons WHERE Id=?", (person_id,))
        person_row = cur.fetchone()
        if not person_row or person_row["SpouseId"]:
            continue

        candidate_ids = name_to_ids.get(p.spouse_name, [])
        spouse_id = None
        if len(candidate_ids) == 1:
            spouse_id = candidate_ids[0]

        if spouse_id is None:
            spouse_gender = 1 if person_row["Gender"] == 0 else 0
            cur.execute(
                """
                INSERT INTO Persons
                (Name, Gender, Generation, BirthDate, ParentId, SpouseId, IsRoot, CreatedAt, UpdatedAt)
                VALUES (?, ?, ?, NULL, NULL, NULL, 0, ?, ?)
                """,
                (
                    p.spouse_name,
                    spouse_gender,
                    p.generation,
                    now,
                    now,
                ),
            )
            spouse_id = cur.lastrowid
            name_to_ids.setdefault(p.spouse_name, []).append(spouse_id)

        cur.execute("UPDATE Persons SET SpouseId=?, UpdatedAt=? WHERE Id=?", (spouse_id, now, person_id))
        cur.execute("UPDATE Persons SET SpouseId=?, UpdatedAt=? WHERE Id=?", (person_id, now, spouse_id))

    # Ensure only one root.
    cur.execute("UPDATE Persons SET IsRoot=0")
    root_id = temp_to_db.get(1)
    if root_id:
        cur.execute("UPDATE Persons SET IsRoot=1 WHERE Id=?", (root_id,))

    cur.execute("PRAGMA foreign_keys=ON;")
    conn.commit()

    count = cur.execute("SELECT COUNT(*) FROM Persons").fetchone()[0]
    roots = cur.execute("SELECT Name FROM Persons WHERE IsRoot=1 LIMIT 1").fetchone()
    print(f"Imported persons: {count}")
    print(f"Root: {roots[0] if roots else 'N/A'}")

    conn.close()


def main() -> None:
    if not XMIND_PATH.exists():
        raise SystemExit(f"XMind file not found: {XMIND_PATH}")
    if not DB_PATH.exists():
        raise SystemExit(f"Database not found: {DB_PATH}")

    people = load_xmind_people(XMIND_PATH)
    print(f"Extracted primary nodes: {len(people)}")
    import_people(DB_PATH, people)


if __name__ == "__main__":
    main()

