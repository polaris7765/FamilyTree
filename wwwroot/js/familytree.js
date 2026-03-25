(() => {
    const apiBase = "/api/person";
    const defaultAvatar = "/images/default-avatar.svg";

    const state = {
        treeData: null,
        persons: [],
        selectedPerson: null,
        zoomBehavior: null,
        svg: null,
        graph: null,
        permissions: {
            role: "Viewer",
            canManageMembers: false
        },
        siteSettings: {
            siteTitle: "族谱管理系统",
            familyName: "周氏家谱",
            primaryColor: "#dc3545",
            defaultZoomPercent: 70,
            enableShare: true,
            layoutOrientation: "Horizontal",
            showCardRank: true,
            showCardGeneration: true,
            showCardGenerationLevel: true
        },
        initialTransform: null
    };

    const el = {};
    let detailModal;
    let editModal;
    let membersModal;
    let statsModal;
    let shareModal;
    let helpModal;

    document.addEventListener("DOMContentLoaded", () => {
        cacheElements();
        initModals();
        bindEvents();
        loadInitialData();
    });

    function cacheElements() {
        [
            "loadingSpinner", "treeSvg", "treeContainer", "searchInput", "searchResults", "searchResultsList", "familyName",
            "btnViewAll", "btnStats", "btnSearch", "btnCloseSearch", "btnAddRoot", "btnExport", "btnImportXmind", "btnExportXmind", "btnShare", "btnPrint",
            "btnHelp",
            "btnZoomIn", "btnZoomOut", "btnZoomReset", "btnEditPerson", "btnAddChild", "btnAddSpouse", "btnDeletePerson",
            "btnSavePerson", "btnGenerateShare", "btnCopyShare", "shareUrl", "statsBody", "membersTableBody",
            "detailPhoto", "detailName", "detailNameText", "detailGenderBadge", "detailGenerationBadge", "detailRankBadge", "detailGender",
            "detailGeneration", "detailOccupation", "detailBirthDate", "detailDeathDate", "detailBirthPlace", "detailCurrentAddress", "detailPhone",
            "detailRank", "detailSpouse", "detailParent", "detailSons", "detailDaughters", "detailBiography", "photoUpload",
            "xmindImportInput",
            "editModalTitle", "editPersonId", "editName", "editGender", "editGeneration", "editBirthDate", "editDeathDate",
            "editOccupation", "editBirthPlace", "editCurrentAddress", "editPhone", "editParentId", "editSpouseId", "editBiography", "editIsRoot"
        ].forEach((id) => {
            el[id] = document.getElementById(id);
        });
    }

    function initModals() {
        detailModal = new bootstrap.Modal(document.getElementById("personDetailModal"));
        editModal = new bootstrap.Modal(document.getElementById("personEditModal"));
        membersModal = new bootstrap.Modal(document.getElementById("allMembersModal"));
        statsModal = new bootstrap.Modal(document.getElementById("statsModal"));
        shareModal = new bootstrap.Modal(document.getElementById("shareModal"));
        helpModal = new bootstrap.Modal(document.getElementById("helpModal"));
    }

    function bindEvents() {
        onClick(el.btnViewAll, showAllMembers);
        onClick(el.btnStats, showStats);
        onClick(el.btnSearch, doSearch);
        onClick(el.btnCloseSearch, () => toggleSearch(false));
        onClick(el.btnAddRoot, () => openEditModal(null, null, null));
        onClick(el.btnExport, exportTreeAsImage);
        onClick(el.btnImportXmind, openXmindImportPicker);
        onClick(el.btnExportXmind, exportXmindFile);
        onClick(el.btnShare, () => shareModal.show());
        onClick(el.btnHelp, () => helpModal.show());
        onClick(el.btnPrint, printTree);
        onClick(el.btnZoomIn, () => applyZoom(1.15));
        onClick(el.btnZoomOut, () => applyZoom(0.85));
        onClick(el.btnZoomReset, resetZoom);
        onClick(el.btnEditPerson, () => openEditModal(state.selectedPerson));
        onClick(el.btnAddChild, () => openEditModal(null, state.selectedPerson?.id ?? null, null));
        onClick(el.btnAddSpouse, () => openEditModal(null, null, state.selectedPerson?.id ?? null));
        onClick(el.btnDeletePerson, deleteSelectedPerson);
        onClick(el.btnSavePerson, savePerson);
        onClick(el.btnGenerateShare, generateShareLink);
        onClick(el.btnCopyShare, copyShareLink);

        el.searchInput?.addEventListener("keydown", (e) => {
            if (e.key === "Enter") doSearch();
        });

        el.photoUpload?.addEventListener("change", uploadPhoto);
        el.xmindImportInput?.addEventListener("change", importXmindFile);
        preventViewportGestureOnTree();
    }

    async function loadInitialData() {
        showLoading(true, "正在加载族谱数据...");
        try {
            await loadPermissions();
            await loadSiteSettings();
            await Promise.all([loadTree(), loadPersons()]);
        } catch (err) {
            showError(`加载失败：${err.message || err}`);
        } finally {
            showLoading(false);
        }
    }

    async function loadTree() {
        const tree = await apiGet(`${apiBase}/tree`);
        if (!tree || !tree.id) {
            showError("当前没有可展示的族谱数据，请先添加成员。");
            return;
        }

        state.treeData = tree;
        if (el.familyName) {
            el.familyName.textContent = state.siteSettings.familyName || "周氏家谱";
        }
        renderTree(tree);
    }

    async function loadSiteSettings() {
        try {
            state.siteSettings = await apiGet(`${apiBase}/site-settings`);
        } catch {
            state.siteSettings = {
                siteTitle: "族谱管理系统",
                familyName: "周氏家谱",
                primaryColor: "#dc3545",
                defaultZoomPercent: 70,
                enableShare: true,
                layoutOrientation: "Horizontal",
                showCardRank: true,
                showCardGeneration: true,
                showCardGenerationLevel: true
            };
        }

        document.title = `${state.siteSettings.siteTitle || "族谱管理系统"} - ${state.siteSettings.familyName || "周氏家谱"}`;

        if (el.familyName) {
            el.familyName.textContent = state.siteSettings.familyName || "周氏家谱";
        }

        const brandColor = state.siteSettings.primaryColor || "#dc3545";
        document.documentElement.style.setProperty("--brand-color", brandColor);
        document.documentElement.style.setProperty("--brand-color-dark", shadeColor(brandColor, -15));

        if (el.btnShare) {
            el.btnShare.style.display = state.siteSettings.enableShare ? "inline-block" : "none";
        }
    }

    async function loadPermissions() {
        try {
            state.permissions = await apiGet(`${apiBase}/permissions`);
        } catch {
            state.permissions = {
                role: "Viewer",
                canManageMembers: false
            };
        }

        const canManage = !!state.permissions.canManageMembers;
        [el.btnAddRoot, el.btnEditPerson, el.btnAddChild, el.btnAddSpouse, el.btnDeletePerson, el.btnImportXmind, el.btnExportXmind]
            .forEach((button) => {
                if (!button) return;
                button.style.display = canManage ? "inline-block" : "none";
            });
    }

    async function loadPersons() {
        state.persons = await apiGet(apiBase);
        fillSelectOptions();
    }

    function renderTree(treeData) {
        if (typeof d3 === "undefined") {
            showError("D3 加载失败，请检查网络后刷新页面。");
            return;
        }

        const width = el.treeContainer?.clientWidth || 1200;
        const height = el.treeContainer?.clientHeight || 800;
        const isHorizontal = state.siteSettings.layoutOrientation === "Horizontal";
        const cardWidth = 192;
        const cardHeight = 76;
        const cardX = -(cardWidth / 2);
        const cardY = -(cardHeight / 2);
        const photoSize = 50;
        const photoX = cardX + 8;
        const photoY = -(photoSize / 2);
        const cardTextX = photoX + photoSize + 10;
        const cardInnerRight = (cardWidth / 2) - 5;
        const badgeSlots = {
            rank: cardY + 7,
            generation: cardY + 25,
            level: cardY + 43
        };

        const root = d3.hierarchy(treeData, (d) => d.children || []);
        const layout = d3.tree().nodeSize(isHorizontal ? [92, 208] : [156, 138]);
        layout(root);

        const positionedNodes = root.descendants().map((node) => ({
            node,
            drawX: isHorizontal ? node.y : node.x,
            drawY: isHorizontal ? node.x : node.y
        }));

        const minX = d3.min(positionedNodes, (d) => d.drawX) ?? 0;
        const maxX = d3.max(positionedNodes, (d) => d.drawX) ?? 0;
        const minY = d3.min(positionedNodes, (d) => d.drawY) ?? 0;
        const maxY = d3.max(positionedNodes, (d) => d.drawY) ?? 0;
        const rootDrawX = isHorizontal ? root.y : root.x;
        const rootDrawY = isHorizontal ? root.x : root.y;

        const svg = d3.select(el.treeSvg);
        svg.selectAll("*").remove();
        svg.style("touch-action", "none");
        svg
            .attr("width", width)
            .attr("height", height)
            .attr("viewBox", `0 0 ${width} ${height}`)
            .attr("preserveAspectRatio", "xMidYMid meet");

        const initialScale = Math.min(3, Math.max(0.45, (state.siteSettings.defaultZoomPercent || 70) / 100));
        const rootAnchorX = isHorizontal ? Math.max(120, Math.min(220, width * 0.18)) : width / 2;
        const desiredTranslateX = rootAnchorX - (rootDrawX * initialScale);
        const minTranslateX = width - ((maxX + cardWidth / 2 + 40) * initialScale);
        const maxTranslateX = 24 - ((minX - cardWidth / 2) * initialScale);
        const baseTranslateX = clamp(desiredTranslateX, minTranslateX, maxTranslateX);

        const rootAnchorY = isHorizontal ? (height / 2) : Math.max(110, Math.min(160, height * 0.16));
        const desiredTranslateY = rootAnchorY - (rootDrawY * initialScale);
        const minTranslateY = height - ((maxY + cardHeight / 2 + 24) * initialScale);
        const maxTranslateY = 24 - ((minY - cardHeight / 2) * initialScale);
        const baseTranslateY = clamp(desiredTranslateY, minTranslateY, maxTranslateY);
        const graph = svg.append("g").attr("transform", `translate(${baseTranslateX}, ${baseTranslateY})`);
        state.svg = svg;
        state.graph = graph;

        state.zoomBehavior = d3.zoom().scaleExtent([0.15, 8]).on("zoom", (event) => {
            graph.attr("transform", event.transform);
        });

        svg.call(state.zoomBehavior);
        state.initialTransform = d3.zoomIdentity.translate(baseTranslateX, baseTranslateY).scale(initialScale);
        svg.call(state.zoomBehavior.transform, state.initialTransform);

        const linkGenerator = isHorizontal
            ? d3.linkHorizontal().x((d) => d.y).y((d) => d.x)
            : d3.linkVertical().x((d) => d.x).y((d) => d.y);

        graph.selectAll("path.tree-link")
            .data(root.links())
            .enter()
            .append("path")
            .attr("class", "tree-link")
            .attr("d", linkGenerator);

        const node = graph.selectAll("g.person-card")
            .data(root.descendants())
            .enter()
            .append("g")
            .attr("class", "person-card")
            .attr("transform", (d) => `translate(${isHorizontal ? d.y : d.x},${isHorizontal ? d.x : d.y})`)
            .on("click", (_, d) => openPersonDetail(d.data.id));

        node.append("rect")
            .attr("class", (d) => {
                if (d.data.isRoot) return "card-bg card-bg-root";
                return d.data.gender === "Female" ? "card-bg card-bg-female" : "card-bg card-bg-male";
            })
            .attr("x", cardX)
            .attr("y", cardY)
            .attr("width", cardWidth)
            .attr("height", cardHeight)
            .attr("rx", 10)
            .attr("ry", 10);

        node.append("text")
            .attr("class", "card-name")
            .attr("text-anchor", "start")
            .attr("x", cardTextX)
            .attr("y", -13)
            .text((d) => `${d.data.name || "未命名"} ${genderIcon(d.data.gender)}`);

        node.append("text")
            .attr("class", "card-info")
            .attr("text-anchor", "start")
            .attr("x", cardTextX)
            .attr("y", 4)
            .text((d) => `配偶 ${d.data.spouse?.name || "-"}`);

        node.append("text")
            .attr("class", "card-info")
            .attr("text-anchor", "start")
            .attr("x", cardTextX)
            .attr("y", 20)
            .text((d) => d.data.occupation ? `职业 ${d.data.occupation}` : `子女 ${d.data.sonsCount ?? 0}/${d.data.daughtersCount ?? 0}`);

        node.filter((d) => !!d.data.photoUrl)
            .each(function (d) {
                const group = d3.select(this);
                const clipId = `card-photo-clip-${d.data.id}`;
                const defs = group.append("defs");

                defs.append("clipPath")
                    .attr("id", clipId)
                    .append("rect")
                    .attr("x", photoX + 1)
                    .attr("y", photoY + 1)
                    .attr("width", photoSize - 2)
                    .attr("height", photoSize - 2)
                    .attr("rx", 7)
                    .attr("ry", 7);

                group.append("rect")
                    .attr("class", "card-photo-frame")
                    .attr("x", photoX)
                    .attr("y", photoY)
                    .attr("width", photoSize)
                    .attr("height", photoSize)
                    .attr("rx", 8)
                    .attr("ry", 8);

                group.append("image")
                    .attr("class", "card-photo")
                    .attr("x", photoX + 1)
                    .attr("y", photoY + 1)
                    .attr("width", photoSize - 2)
                    .attr("height", photoSize - 2)
                    .attr("href", d.data.photoUrl)
                    .attr("clip-path", `url(#${clipId})`)
                    .attr("preserveAspectRatio", "xMidYMid slice");
            });

        node.each(function (d) {
            const badgeGroup = d3.select(this).append("g").attr("class", "card-right-badges");
            const rightEdge = cardInnerRight;
            const slots = [
                {
                    type: "rank",
                    prefix: "行",
                    value: d.data.rank ? `${d.data.rank}` : "",
                    y: badgeSlots.rank,
                    prefixFontSize: 8,
                    valueFontSize: 8.6,
                    height: 16,
                    paddingX: 5,
                    minWidth: 22,
                    radius: 5,
                    visible: !!state.siteSettings.showCardRank && d.data.gender === "Male" && !!d.data.rank
                },
                {
                    type: "generation",
                    prefix: "辈",
                    value: d.data.generation || "",
                    y: badgeSlots.generation,
                    prefixFontSize: 7,
                    valueFontSize: 8.6,
                    height: 16,
                    paddingX: 5,
                    minWidth: 22,
                    radius: 5,
                    visible: !!state.siteSettings.showCardGeneration && !!d.data.generation
                },
                {
                    type: "level",
                    prefix: "",
                    value: `第 ${d.depth + 1} 代`,
                    y: badgeSlots.level,
                    prefixFontSize: 0,
                    valueFontSize: 8.5,
                    height: 18,
                    paddingX: 6,
                    minWidth: 46,
                    radius: 5,
                    visible: !!state.siteSettings.showCardGenerationLevel
                }
            ];

            slots.forEach((item) => {
                if (!item.visible) return;

                const innerPaddingX = item.paddingX;
                const chipHeight = item.height;
                const valueWidth = measureBadgeTextWidth(item.value, item.valueFontSize, 700) + 1;
                const chipWidth = Math.max(item.minWidth, Math.ceil((innerPaddingX * 2) + valueWidth));
                const chipX = rightEdge - chipWidth;
                const valueX = chipX + (chipWidth / 2);
                const prefixX = chipX - 3;
                const centerY = item.y + (chipHeight / 2);

                badgeGroup.append("rect")
                    .attr("class", item.type === "rank"
                        ? "card-rank-box"
                        : item.type === "generation"
                            ? "card-generation-chip"
                            : "card-level-chip")
                    .attr("x", chipX)
                    .attr("y", item.y)
                    .attr("width", chipWidth)
                    .attr("height", chipHeight)
                    .attr("rx", item.radius)
                    .attr("ry", item.radius);

                if (item.prefix) {
                    badgeGroup.append("text")
                        .attr("class", item.type === "rank" ? "card-rank-prefix" : "card-generation-prefix")
                        .attr("text-anchor", "end")
                        .attr("dominant-baseline", "middle")
                        .attr("x", prefixX)
                        .attr("y", centerY)
                        .text(item.prefix);
                }

                badgeGroup.append("text")
                    .attr("class", item.type === "rank"
                        ? "card-rank-text"
                        : item.type === "generation"
                            ? "card-generation-chip-text"
                            : "card-level-chip-text")
                    .attr("text-anchor", "middle")
                    .attr("dominant-baseline", "middle")
                    .attr("x", valueX)
                    .attr("y", centerY)
                    .text(item.value);
            });
        });
    }

    function fillSelectOptions() {
        const options = ["<option value=''>-- 无 --</option>"]
            .concat(state.persons.map((p) => `<option value='${p.id}'>${escapeHtml(p.name)}</option>`))
            .join("");

        if (el.editParentId) el.editParentId.innerHTML = options;
        if (el.editSpouseId) el.editSpouseId.innerHTML = options;
    }

    async function openPersonDetail(personId) {
        const person = state.persons.find((p) => p.id === personId) || await apiGet(`${apiBase}/${personId}`);
        state.selectedPerson = person;

        setText(el.detailName, person.name);
        setText(el.detailNameText, person.name);
        setText(el.detailGender, genderText(person.gender));
        setText(el.detailGeneration, person.generation || "-");
        setText(el.detailOccupation, person.occupation || "-");
        setText(el.detailBirthDate, formatDate(person.birthDate));
        setText(el.detailDeathDate, formatDate(person.deathDate));
        setText(el.detailBirthPlace, person.birthPlace || "-");
        setText(el.detailCurrentAddress, person.currentAddress || "-");
        setText(el.detailPhone, person.phone || "-");
        setText(el.detailRank, person.gender === "Male" ? (person.rank ? `行${person.rank}` : "-") : "-");
        setText(el.detailSons, `${person.sonsCount ?? 0}`);
        setText(el.detailDaughters, `${person.daughtersCount ?? 0}`);
        setText(el.detailBiography, person.biography || "-");

        if (el.detailGenderBadge) {
            el.detailGenderBadge.className = `badge ${person.gender === "Female" ? "bg-danger" : "bg-primary"}`;
            el.detailGenderBadge.textContent = genderText(person.gender);
            el.detailGenderBadge.style.display = "inline-block";
        }
        if (el.detailGenerationBadge) {
            if (person.gender === "Male") {
                el.detailGenerationBadge.style.display = "inline-block";
                el.detailGenerationBadge.textContent = person.generation ? `${person.generation}辈` : "未设辈分";
            } else {
                el.detailGenerationBadge.style.display = "none";
                el.detailGenerationBadge.textContent = "";
            }
        }
        if (el.detailRankBadge) {
            if (person.gender === "Male") {
                el.detailRankBadge.style.display = "inline-block";
                el.detailRankBadge.textContent = person.rank ? `行${person.rank}` : "未排行";
            } else {
                el.detailRankBadge.style.display = "none";
                el.detailRankBadge.textContent = "";
            }
        }
        bindRelatedPersonLink(el.detailSpouse, person.spouseId, person.spouseName);
        bindRelatedPersonLink(el.detailParent, person.parentId, person.parentName);
        if (el.detailPhoto) {
            el.detailPhoto.src = person.photoUrl || defaultAvatar;
        }

        detailModal.show();
    }

    function openEditModal(person = null, parentId = null, spouseId = null) {
        const isEdit = !!person;
        if (el.editModalTitle) {
            el.editModalTitle.textContent = isEdit ? "编辑成员" : "添加成员";
        }

        setInputValue(el.editPersonId, person?.id ?? "");
        setInputValue(el.editName, person?.name ?? "");
        setInputValue(el.editGender, person?.gender ?? "Male");
        setInputValue(el.editGeneration, person?.generation ?? "");
        setInputValue(el.editBirthDate, dateForInput(person?.birthDate));
        setInputValue(el.editDeathDate, dateForInput(person?.deathDate));
        setInputValue(el.editOccupation, person?.occupation ?? "");
        setInputValue(el.editBirthPlace, person?.birthPlace ?? "");
        setInputValue(el.editCurrentAddress, person?.currentAddress ?? "");
        setInputValue(el.editPhone, person?.phone ?? "");
        setInputValue(el.editBiography, person?.biography ?? "");
        if (el.editIsRoot) el.editIsRoot.checked = !!person?.isRoot;

        setInputValue(el.editParentId, person?.parentId?.toString() ?? (parentId ? String(parentId) : ""));
        setInputValue(el.editSpouseId, person?.spouseId?.toString() ?? (spouseId ? String(spouseId) : ""));

        editModal.show();
    }

    async function savePerson() {
        const id = el.editPersonId.value;
        const payload = {
            name: el.editName.value.trim(),
            gender: el.editGender.value,
            generation: valueOrNull(el.editGeneration.value),
            birthDate: valueOrNull(el.editBirthDate.value),
            deathDate: valueOrNull(el.editDeathDate.value),
            occupation: valueOrNull(el.editOccupation.value),
            birthPlace: valueOrNull(el.editBirthPlace.value),
            currentAddress: valueOrNull(el.editCurrentAddress.value),
            phone: valueOrNull(el.editPhone.value),
            biography: valueOrNull(el.editBiography.value),
            parentId: valueOrInt(el.editParentId.value),
            spouseId: valueOrInt(el.editSpouseId.value),
            isRoot: !!el.editIsRoot.checked
        };

        if (!payload.name) {
            alert("姓名不能为空");
            return;
        }

        try {
            if (id) {
                await apiSend(`${apiBase}/${id}`, "PUT", payload);
            } else {
                await apiSend(apiBase, "POST", payload);
            }

            editModal.hide();
            await refreshData();
        } catch (err) {
            alert(`保存失败：${err.message || err}`);
        }
    }

    async function deleteSelectedPerson() {
        if (!state.selectedPerson) return;
        if (!confirm(`确认删除 ${state.selectedPerson.name} 吗？`)) return;

        try {
            await apiSend(`${apiBase}/${state.selectedPerson.id}`, "DELETE");
            detailModal.hide();
            await refreshData();
        } catch (err) {
            alert(`删除失败：${err.message || err}`);
        }
    }

    async function doSearch() {
        const keyword = el.searchInput.value.trim();
        if (!keyword) {
            toggleSearch(false);
            return;
        }

        const results = await apiGet(`${apiBase}/search?name=${encodeURIComponent(keyword)}`);
        if (!results.length) {
            el.searchResultsList.innerHTML = "<div class='p-3 text-muted'>未找到匹配成员</div>";
            toggleSearch(true);
            return;
        }

        el.searchResultsList.innerHTML = results.map((p) => {
            const avatarClass = p.gender === "Female" ? "female" : "male";
            return `<div class='search-result-item' data-id='${p.id}'>
                        <div class='avatar ${avatarClass}'>${p.gender === "Female" ? "女" : "男"}</div>
                        <div class='info'>
                            <div class='name'>${escapeHtml(p.name)}</div>
                            <div class='detail'>${escapeHtml(p.generation || "-")}辈 · ${escapeHtml(p.parentName || "无父节点")}</div>
                        </div>
                    </div>`;
        }).join("");

        el.searchResultsList.querySelectorAll(".search-result-item").forEach((item) => {
            item.addEventListener("click", () => {
                const id = Number(item.getAttribute("data-id"));
                openPersonDetail(id);
                toggleSearch(false);
            });
        });

        toggleSearch(true);
    }

    async function showAllMembers() {
        if (!state.persons.length) {
            await loadPersons();
        }

        el.membersTableBody.innerHTML = state.persons.map((p) => `
            <tr>
                <td>${escapeHtml(p.name)}</td>
                <td>${genderText(p.gender)}</td>
                <td>${escapeHtml(p.generation || "-")}</td>
                <td>${escapeHtml(p.occupation || "-")}</td>
                <td>${formatDate(p.birthDate)}</td>
                <td>${p.spouseId ? `<span class='spouse-link' data-action='view-spouse' data-id='${p.spouseId}'>${escapeHtml(p.spouseName || "-")}</span>` : "-"}</td>
                <td>${escapeHtml(p.parentName || "-")}</td>
                <td>${p.gender === "Male" ? (p.rank ? `行${p.rank}` : "-") : "-"}</td>
                <td>${p.sonsCount ?? 0}</td>
                <td>${p.daughtersCount ?? 0}</td>
                <td><button class='btn btn-sm btn-outline-primary' data-action='view' data-id='${p.id}'>查看</button></td>
            </tr>
        `).join("");

        el.membersTableBody.querySelectorAll("button[data-action='view']").forEach((btn) => {
            btn.addEventListener("click", () => {
                const id = Number(btn.getAttribute("data-id"));
                membersModal.hide();
                openPersonDetail(id);
            });
        });

        el.membersTableBody.querySelectorAll("[data-action='view-spouse']").forEach((item) => {
            item.addEventListener("click", () => {
                const id = Number(item.getAttribute("data-id"));
                membersModal.hide();
                openPersonDetail(id);
            });
        });

        membersModal.show();
    }

    async function showStats() {
        const stats = await apiGet(`${apiBase}/stats`);
        const generations = (stats.generations || []).map((g) => `<span class='badge bg-secondary me-1'>${escapeHtml(g)}</span>`).join("") || "-";

        el.statsBody.innerHTML = `
            <div class='mb-2'><strong>总人数：</strong>${stats.totalMembers ?? 0}</div>
            <div class='mb-2'><strong>男性：</strong>${stats.maleCount ?? 0}</div>
            <div class='mb-2'><strong>女性：</strong>${stats.femaleCount ?? 0}</div>
            <div class='mb-2'><strong>辈分数量：</strong>${stats.generationCount ?? 0}</div>
            <div><strong>辈分：</strong>${generations}</div>
        `;

        statsModal.show();
    }

    async function generateShareLink() {
        const data = await apiSend(`${apiBase}/share`, "POST");
        el.shareUrl.value = data.url || "";
    }

    async function copyShareLink() {
        if (!el.shareUrl.value) return;
        await navigator.clipboard.writeText(el.shareUrl.value);
        alert("分享链接已复制");
    }

    async function uploadPhoto(event) {
        if (!state.selectedPerson || !event.target.files?.length) return;

        const formData = new FormData();
        formData.append("file", event.target.files[0]);

        const response = await fetch(`${apiBase}/${state.selectedPerson.id}/photo`, {
            method: "POST",
            body: formData
        });

        if (!response.ok) {
            const text = await response.text();
            alert(`上传失败：${text || response.statusText}`);
            return;
        }

        await refreshData();
        await openPersonDetail(state.selectedPerson.id);
    }

    function openXmindImportPicker() {
        if (el.xmindImportInput) {
            el.xmindImportInput.value = "";
            el.xmindImportInput.click();
        }
    }

    async function importXmindFile(event) {
        const file = event.target?.files?.[0];
        if (!file) return;

        if (!confirm("导入 XMind 会替换当前族谱成员数据，是否继续？")) {
            event.target.value = "";
            return;
        }

        const formData = new FormData();
        formData.append("file", file);

        const response = await fetch(`${apiBase}/import-xmind`, {
            method: "POST",
            body: formData,
            headers: { "Accept": "application/json" }
        });

        if (!response.ok) {
            throw new Error(await readError(response));
        }

        const result = await response.json();
        alert(`XMind 导入成功，共导入 ${result.importedCount ?? 0} 人`);
        event.target.value = "";
        await refreshData();
    }

    async function exportXmindFile() {
        const response = await fetch(`${apiBase}/export-xmind`, {
            cache: "no-store",
            headers: { "Accept": "application/vnd.xmind.workbook" }
        });

        if (!response.ok) {
            throw new Error(await readError(response));
        }

        const blob = await response.blob();
        const disposition = response.headers.get("Content-Disposition") || "";
        const fileNameMatch = disposition.match(/filename\*?=(?:UTF-8''|")?([^";]+)/i);
        const fileName = fileNameMatch ? decodeURIComponent(fileNameMatch[1].replace(/"/g, "")) : `family-tree-${Date.now()}.xmind`;
        downloadBlob(blob, fileName);
    }

    async function exportTreeAsImage() {
        try {
            const { dataUrl } = await exportSvgTreeToPng(el.treeSvg, 2.5);
            const link = document.createElement("a");
            link.download = `family-tree-${Date.now()}.png`;
            link.href = dataUrl;
            link.click();
        } catch (err) {
            // Fallback for unexpected browsers: keep old html2canvas behavior.
            if (typeof html2canvas === "undefined") {
                alert("导出组件未加载，请检查网络连接。");
                return;
            }

            const canvas = await html2canvas(el.treeContainer, {
                backgroundColor: "#ffffff",
                scale: 2
            });

            const link = document.createElement("a");
            link.download = `family-tree-${Date.now()}.png`;
            link.href = canvas.toDataURL("image/png");
            link.click();
            console.warn("SVG export fallback to html2canvas:", err);
        }
    }

    async function printTree() {
        const { dataUrl } = await exportSvgTreeToPng(el.treeSvg, 2.5);
        const pageOrientation = state.siteSettings.layoutOrientation === "Horizontal" ? "landscape" : "portrait";
        const printFrame = getOrCreatePrintFrame();
        const printDoc = printFrame.contentDocument || printFrame.contentWindow?.document;
        if (!printDoc || !printFrame.contentWindow) {
            throw new Error("打印环境初始化失败");
        }

        const cleanup = () => {
            setTimeout(() => {
                printFrame.remove();
            }, 300);
        };

        printFrame.onload = () => {
            const frameWindow = printFrame.contentWindow;
            if (!frameWindow) {
                cleanup();
                return;
            }

            frameWindow.onafterprint = cleanup;
        };

        printDoc.open();
        printDoc.write(`<!doctype html>
<html lang="zh-CN">
<head>
  <meta charset="utf-8">
  <title>${escapeHtml(state.siteSettings.familyName || "族谱打印")}</title>
  <style>
    @page { size: A4 ${pageOrientation}; margin: 5mm; }
    html, body { margin: 0; padding: 0; background: #fff; }
    body { display: flex; align-items: center; justify-content: center; }
    .print-wrap { width: 100%; }
    img { display: block; width: 100%; height: auto; page-break-inside: avoid; }
  </style>
</head>
<body>
  <div class="print-wrap">
    <img src="${dataUrl}" alt="族谱打印图">
  </div>
  <script>
    window.onload = function () {
      setTimeout(function () {
        window.focus();
        window.print();
      }, 120);
    };

    window.onafterprint = function () {
      try {
        var frame = window.parent && window.parent.document.getElementById("treePrintFrame");
        if (frame) {
          setTimeout(function () { frame.remove(); }, 300);
        }
      } catch (e) {
        // ignore cross-window cleanup errors
      }
    };
  <\/script>
</body>
</html>`);
        printDoc.close();

        setTimeout(cleanup, 60000);
    }

    async function exportSvgTreeToPng(svgElement, scale = 2) {
        if (!svgElement) {
            throw new Error("SVG not found");
        }

        const graphNode = state.graph?.node?.() || svgElement.querySelector("g");
        const graphBounds = graphNode?.getBBox?.();
        const exportPadding = 24;
        const clonedSvg = svgElement.cloneNode(true);
        let exportWidth = svgElement.clientWidth || svgElement.viewBox?.baseVal?.width || 1;
        let exportHeight = svgElement.clientHeight || svgElement.viewBox?.baseVal?.height || 1;

        if (graphBounds && Number.isFinite(graphBounds.width) && Number.isFinite(graphBounds.height) && graphBounds.width > 0 && graphBounds.height > 0) {
            exportWidth = Math.ceil(graphBounds.width + (exportPadding * 2));
            exportHeight = Math.ceil(graphBounds.height + (exportPadding * 2));
            clonedSvg.setAttribute(
                "viewBox",
                `${graphBounds.x - exportPadding} ${graphBounds.y - exportPadding} ${exportWidth} ${exportHeight}`
            );
            clonedSvg.setAttribute("width", `${exportWidth}`);
            clonedSvg.setAttribute("height", `${exportHeight}`);
        }
        clonedSvg.setAttribute("overflow", "visible");
        inlineComputedSvgStyles(svgElement, clonedSvg);
        await inlineSvgImagesAsDataUrl(clonedSvg);

        let svgText = new XMLSerializer().serializeToString(clonedSvg);
        if (!svgText.includes("xmlns=\"http://www.w3.org/2000/svg\"")) {
            svgText = svgText.replace("<svg", "<svg xmlns=\"http://www.w3.org/2000/svg\"");
        }
        if (!svgText.includes("xmlns:xlink=\"http://www.w3.org/1999/xlink\"")) {
            svgText = svgText.replace("<svg", "<svg xmlns:xlink=\"http://www.w3.org/1999/xlink\"");
        }

        const blob = new Blob([svgText], { type: "image/svg+xml;charset=utf-8" });
        const url = URL.createObjectURL(blob);

        try {
            const image = await loadImage(url);
            const canvas = document.createElement("canvas");
            canvas.width = Math.round(exportWidth * scale);
            canvas.height = Math.round(exportHeight * scale);

            const ctx = canvas.getContext("2d");
            if (!ctx) throw new Error("Canvas context unavailable");

            ctx.fillStyle = "#ffffff";
            ctx.fillRect(0, 0, canvas.width, canvas.height);
            ctx.drawImage(image, 0, 0, canvas.width, canvas.height);
            return {
                dataUrl: canvas.toDataURL("image/png"),
                width: exportWidth,
                height: exportHeight
            };
        } finally {
            URL.revokeObjectURL(url);
        }
    }

    function inlineComputedSvgStyles(sourceSvg, targetSvg) {
        const sourceNodes = sourceSvg.querySelectorAll("*");
        const targetNodes = targetSvg.querySelectorAll("*");
        const props = [
            "fill", "fill-opacity",
            "stroke", "stroke-width", "stroke-opacity", "stroke-dasharray", "stroke-linecap", "stroke-linejoin",
            "opacity", "filter", "clip-path",
            "rx", "ry",
            "font-family", "font-size", "font-weight", "font-style", "text-anchor", "dominant-baseline"
        ];

        for (let i = 0; i < sourceNodes.length && i < targetNodes.length; i += 1) {
            const sourceNode = sourceNodes[i];
            const targetNode = targetNodes[i];
            const computed = window.getComputedStyle(sourceNode);
            const styleFragments = [];

            props.forEach((prop) => {
                const value = computed.getPropertyValue(prop);
                if (value && value.trim()) {
                    styleFragments.push(`${prop}:${value}`);
                }
            });

            if (styleFragments.length > 0) {
                const existing = targetNode.getAttribute("style") || "";
                targetNode.setAttribute("style", `${existing};${styleFragments.join(";")}`);
            }
        }
    }

    async function inlineSvgImagesAsDataUrl(svgRoot) {
        const imageNodes = Array.from(svgRoot.querySelectorAll("image"));
        await Promise.all(imageNodes.map(async (node) => {
            const rawHref = node.getAttribute("href") || node.getAttributeNS("http://www.w3.org/1999/xlink", "href");
            if (!rawHref || rawHref.startsWith("data:")) return;

            const absoluteUrl = new URL(rawHref, window.location.origin).toString();
            const response = await fetch(absoluteUrl, { cache: "no-store" });
            if (!response.ok) return;

            const blob = await response.blob();
            const dataUrl = await blobToDataUrl(blob);
            node.setAttribute("href", dataUrl);
            node.setAttributeNS("http://www.w3.org/1999/xlink", "href", dataUrl);
        }));
    }

    function blobToDataUrl(blob) {
        return new Promise((resolve, reject) => {
            const reader = new FileReader();
            reader.onload = () => resolve(reader.result);
            reader.onerror = () => reject(reader.error || new Error("Failed to read blob"));
            reader.readAsDataURL(blob);
        });
    }

    function loadImage(src) {
        return new Promise((resolve, reject) => {
            const image = new Image();
            image.onload = () => resolve(image);
            image.onerror = () => reject(new Error("Failed to load image for export"));
            image.src = src;
        });
    }

    function downloadBlob(blob, fileName) {
        const url = URL.createObjectURL(blob);
        const link = document.createElement("a");
        link.href = url;
        link.download = fileName;
        link.click();
        setTimeout(() => URL.revokeObjectURL(url), 0);
    }

    function getOrCreatePrintFrame() {
        const existing = document.getElementById("treePrintFrame");
        if (existing) {
            existing.remove();
        }

        const frame = document.createElement("iframe");
        frame.id = "treePrintFrame";
        frame.setAttribute("aria-hidden", "true");
        frame.style.position = "fixed";
        frame.style.right = "0";
        frame.style.bottom = "0";
        frame.style.width = "0";
        frame.style.height = "0";
        frame.style.border = "0";
        frame.style.opacity = "0";
        frame.style.pointerEvents = "none";
        document.body.appendChild(frame);
        return frame;
    }

    function applyZoom(ratio) {
        if (!state.svg || !state.zoomBehavior) return;
        state.svg.transition().duration(200).call(state.zoomBehavior.scaleBy, ratio);
    }

    function resetZoom() {
        if (!state.svg || !state.zoomBehavior) return;
        const fallbackScale = Math.min(3, Math.max(0.45, (state.siteSettings.defaultZoomPercent || 70) / 100));
        const transform = state.initialTransform || d3.zoomIdentity.translate(120, 80).scale(fallbackScale);
        state.svg.transition().duration(250).call(state.zoomBehavior.transform, transform);
    }

    function clamp(value, min, max) {
        if (!Number.isFinite(value)) return min;
        if (!Number.isFinite(min) || !Number.isFinite(max)) return value;
        if (min > max) return (min + max) / 2;
        return Math.min(Math.max(value, min), max);
    }

    function preventViewportGestureOnTree() {
        if (!el.treeContainer) return;

        ["gesturestart", "gesturechange", "gestureend"].forEach((eventName) => {
            el.treeContainer.addEventListener(eventName, (event) => {
                event.preventDefault();
            }, { passive: false });
        });
    }

    async function refreshData() {
        showLoading(true, "正在刷新数据...");
        try {
            await Promise.all([loadTree(), loadPersons()]);
            if (document.getElementById("statsModal")?.classList.contains("show")) {
                await showStats();
            }
        } finally {
            showLoading(false);
        }
    }

    async function apiGet(url) {
        const response = await fetch(url, {
            cache: "no-store",
            headers: { "Accept": "application/json" }
        });
        if (!response.ok) throw new Error(await readError(response));
        return await response.json();
    }

    async function apiSend(url, method, body = null) {
        const options = {
            method,
            cache: "no-store",
            headers: {
                "Accept": "application/json"
            }
        };

        if (body !== null) {
            options.headers["Content-Type"] = "application/json";
            options.body = JSON.stringify(body);
        }

        const response = await fetch(url, options);
        if (!response.ok) throw new Error(await readError(response));

        if (response.status === 204) return null;
        return await response.json();
    }

    async function readError(response) {
        try {
            return await response.text() || `HTTP ${response.status}`;
        } catch {
            return `HTTP ${response.status}`;
        }
    }

    function showLoading(visible, message = "") {
        if (!el.loadingSpinner) return;
        el.loadingSpinner.style.display = visible ? "block" : "none";
        if (message) {
            const p = el.loadingSpinner.querySelector("p");
            if (p) p.textContent = message;
        }
    }

    function showError(message) {
        showLoading(true, message);
    }

    function toggleSearch(visible) {
        if (!el.searchResults) return;
        el.searchResults.style.display = visible ? "block" : "none";
    }

    function onClick(element, handler) {
        element?.addEventListener("click", async (e) => {
            e.preventDefault();
            try {
                await handler();
            } catch (err) {
                alert(`操作失败：${err.message || err}`);
            }
        });
    }

    function valueOrNull(v) {
        const value = (v || "").trim();
        return value === "" ? null : value;
    }

    function valueOrInt(v) {
        const value = (v || "").trim();
        if (!value) return null;
        const parsed = Number(value);
        return Number.isFinite(parsed) ? parsed : null;
    }

    function setInputValue(element, value) {
        if (!element) return;
        element.value = value;
    }

    function setText(element, value) {
        if (element) element.textContent = value ?? "";
    }

    function bindRelatedPersonLink(element, relatedPersonId, relatedPersonName) {
        if (!element) return;

        if (relatedPersonId && relatedPersonName) {
            element.textContent = relatedPersonName;
            element.classList.add("spouse-link");
            element.setAttribute("role", "button");
            element.setAttribute("tabindex", "0");
            element.onclick = () => openPersonDetail(relatedPersonId);
            element.onkeydown = (event) => {
                if (event.key === "Enter" || event.key === " ") {
                    event.preventDefault();
                    openPersonDetail(relatedPersonId);
                }
            };
            return;
        }

        element.textContent = "-";
        element.classList.remove("spouse-link");
        element.removeAttribute("role");
        element.removeAttribute("tabindex");
        element.onclick = null;
        element.onkeydown = null;
    }

    function genderText(gender) {
        return gender === "Female" ? "女" : "男";
    }

    function genderIcon(gender) {
        return gender === "Female" ? "♀" : "♂";
    }

    function formatDate(value) {
        if (!value) return "-";
        const date = new Date(value);
        if (Number.isNaN(date.getTime())) return "-";
        return date.toLocaleDateString("zh-CN");
    }

    function dateForInput(value) {
        if (!value) return "";
        const date = new Date(value);
        if (Number.isNaN(date.getTime())) return "";
        return date.toISOString().slice(0, 10);
    }

    function escapeHtml(text) {
        return String(text)
            .replace(/&/g, "&amp;")
            .replace(/</g, "&lt;")
            .replace(/>/g, "&gt;")
            .replace(/"/g, "&quot;")
            .replace(/'/g, "&#39;");
    }

    function measureBadgeTextWidth(text, fontSize, fontWeight = 700) {
        if (!text) return 0;

        if (!measureBadgeTextWidth.canvas) {
            measureBadgeTextWidth.canvas = document.createElement("canvas");
            measureBadgeTextWidth.context = measureBadgeTextWidth.canvas.getContext("2d");
        }

        const ctx = measureBadgeTextWidth.context;
        if (!ctx) {
            return text.length * fontSize;
        }

        ctx.font = `${fontWeight} ${fontSize}px "Microsoft YaHei", "SimHei", sans-serif`;
        return ctx.measureText(text).width;
    }

    function shadeColor(hex, percent) {
        const normalized = hex.replace("#", "");
        if (!/^[0-9a-fA-F]{6}$/.test(normalized)) return "#c82333";

        const num = parseInt(normalized, 16);
        const amt = Math.round(2.55 * percent);
        const r = Math.min(255, Math.max(0, (num >> 16) + amt));
        const g = Math.min(255, Math.max(0, ((num >> 8) & 0x00ff) + amt));
        const b = Math.min(255, Math.max(0, (num & 0x0000ff) + amt));
        return `#${((1 << 24) + (r << 16) + (g << 8) + b).toString(16).slice(1)}`;
    }
})();

