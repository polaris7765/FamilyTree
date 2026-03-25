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
            defaultZoomPercent: 100,
            enableShare: true
        }
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
            "btnViewAll", "btnStats", "btnSearch", "btnCloseSearch", "btnAddRoot", "btnExport", "btnShare", "btnPrint",
            "btnHelp",
            "btnZoomIn", "btnZoomOut", "btnZoomReset", "btnEditPerson", "btnAddChild", "btnAddSpouse", "btnDeletePerson",
            "btnSavePerson", "btnGenerateShare", "btnCopyShare", "shareUrl", "statsBody", "membersTableBody",
            "detailPhoto", "detailName", "detailNameText", "detailGenderBadge", "detailGenerationBadge", "detailRankBadge", "detailGender",
            "detailGeneration", "detailBirthDate", "detailDeathDate", "detailBirthPlace", "detailCurrentAddress", "detailPhone",
            "detailRank", "detailSpouse", "detailParent", "detailSons", "detailDaughters", "detailBiography", "photoUpload",
            "editModalTitle", "editPersonId", "editName", "editGender", "editGeneration", "editBirthDate", "editDeathDate",
            "editBirthPlace", "editCurrentAddress", "editPhone", "editParentId", "editSpouseId", "editBiography", "editIsRoot"
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
        onClick(el.btnShare, () => shareModal.show());
        onClick(el.btnHelp, () => helpModal.show());
        onClick(el.btnPrint, () => window.print());
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
                defaultZoomPercent: 100,
                enableShare: true
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
            const permissionsData = await apiGet(`${apiBase}/permissions`);
            state.permissions = permissionsData;
        } catch {
            state.permissions = {
                role: "Viewer",
                canManageMembers: false
            };
        }

        const canManage = !!state.permissions.canManageMembers;
        [el.btnAddRoot, el.btnEditPerson, el.btnAddChild, el.btnAddSpouse, el.btnDeletePerson]
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

        const root = d3.hierarchy(treeData, (d) => d.children || []);
        const layout = d3.tree().nodeSize([172, 104]);
        layout(root);

        const minX = d3.min(root.descendants(), (d) => d.x) ?? 0;
        const maxX = d3.max(root.descendants(), (d) => d.x) ?? 0;
        const minY = d3.min(root.descendants(), (d) => d.y) ?? 0;
        const maxY = d3.max(root.descendants(), (d) => d.y) ?? 0;

        const treeWidth = maxX - minX + 300;
        const treeHeight = maxY - minY + 240;

        const svg = d3.select(el.treeSvg);
        svg.selectAll("*").remove();
        svg.style("touch-action", "none");
        svg.attr("viewBox", `0 0 ${Math.max(width, treeWidth)} ${Math.max(height, treeHeight)}`);

        const graph = svg.append("g").attr("transform", `translate(${150 - minX}, ${100 - minY})`);
        state.svg = svg;
        state.graph = graph;

        state.zoomBehavior = d3.zoom().scaleExtent([0.3, 3]).on("zoom", (event) => {
            graph.attr("transform", event.transform);
        });

        svg.call(state.zoomBehavior);
        const initialScale = Math.min(2, Math.max(0.5, (state.siteSettings.defaultZoomPercent || 100) / 100));
        svg.call(state.zoomBehavior.transform, d3.zoomIdentity.translate(150 - minX, 100 - minY).scale(initialScale));

        graph.selectAll("path.tree-link")
            .data(root.links())
            .enter()
            .append("path")
            .attr("class", "tree-link")
            .attr("d", d3.linkVertical().x((d) => d.x).y((d) => d.y));

        const node = graph.selectAll("g.person-card")
            .data(root.descendants())
            .enter()
            .append("g")
            .attr("class", "person-card")
            .attr("transform", (d) => `translate(${d.x},${d.y})`)
            .on("click", (_, d) => openPersonDetail(d.data.id));

        node.append("rect")
            .attr("class", (d) => {
                if (d.data.isRoot) return "card-bg card-bg-root";
                return d.data.gender === "Female" ? "card-bg card-bg-female" : "card-bg card-bg-male";
            })
            .attr("x", -72)
            .attr("y", -26)
            .attr("width", 144)
            .attr("height", 58);

        node.append("text")
            .attr("class", "card-name")
            .attr("text-anchor", "start")
            .attr("x", -32)
            .attr("y", -7)
            .text((d) => `${d.data.name || "未命名"} ${genderIcon(d.data.gender)}`);

        node.append("text")
            .attr("class", "card-info")
            .attr("text-anchor", "start")
            .attr("x", -32)
            .attr("y", 5)
            .text((d) => `配偶 ${d.data.spouse?.name || "-"}`);

        node.append("text")
            .attr("class", "card-info")
            .attr("text-anchor", "start")
            .attr("x", -32)
            .attr("y", 14)
            .text((d) => `子女 ${d.data.sonsCount ?? 0}/${d.data.daughtersCount ?? 0}`);

        // Show uploaded portrait on the left blank area of card.
        const photoNodes = node.filter((d) => !!d.data.photoUrl);
        photoNodes.append("rect")
            .attr("class", "card-photo-frame")
            .attr("x", -66)
            .attr("y", -20)
            .attr("width", 24)
            .attr("height", 24)
            .attr("rx", 4)
            .attr("ry", 4);

        photoNodes.append("image")
            .attr("class", "card-photo")
            .attr("x", -65)
            .attr("y", -19)
            .attr("width", 22)
            .attr("height", 22)
            .attr("href", (d) => d.data.photoUrl)
            .attr("preserveAspectRatio", "xMidYMid slice");

        node.each(function (d) {
            const badgeGroup = d3.select(this).append("g").attr("class", "card-right-badges");
            const badgeItems = [];

            if (d.data.gender === "Male" && d.data.rank) {
                badgeItems.push({ type: "rank", value: `${d.data.rank}`, width: 22 });
            }
            if (d.data.generation) {
                badgeItems.push({ type: "generation", value: `${d.data.generation}`, width: 28 });
            }
            badgeItems.push({ type: "level", value: `第 ${d.depth + 1} 代`, width: 44 });

            const chipHeight = 16;
            const gap = 2;
            const totalHeight = (badgeItems.length * chipHeight) + ((badgeItems.length - 1) * gap);
            let topY = -26 + ((58 - totalHeight) / 2);

            badgeItems.forEach((item) => {
                const rightEdge = 67; // card right edge(72) - 5px margin
                const chipX = rightEdge - item.width;

                if (item.type === "rank") {
                    badgeGroup.append("text")
                        .attr("class", "card-rank-prefix")
                        .attr("text-anchor", "middle")
                        .attr("x", chipX - 7)
                        .attr("y", topY + 12)
                        .text("行");

                    badgeGroup.append("rect")
                        .attr("class", "card-rank-box")
                        .attr("x", chipX)
                        .attr("y", topY)
                        .attr("width", item.width)
                        .attr("height", chipHeight);

                    badgeGroup.append("text")
                        .attr("class", "card-rank-text")
                        .attr("text-anchor", "middle")
                        .attr("x", chipX + (item.width / 2))
                        .attr("y", topY + 12)
                        .text(item.value);
                }

                if (item.type === "generation") {
                    badgeGroup.append("text")
                        .attr("class", "card-generation-prefix")
                        .attr("text-anchor", "middle")
                        .attr("x", chipX - 7)
                        .attr("y", topY + 13)
                        .text("辈");

                    badgeGroup.append("rect")
                        .attr("class", "card-generation-chip")
                        .attr("x", chipX)
                        .attr("y", topY)
                        .attr("width", item.width)
                        .attr("height", chipHeight);

                    badgeGroup.append("text")
                        .attr("class", "card-generation-chip-text")
                        .attr("text-anchor", "middle")
                        .attr("x", chipX + (item.width / 2))
                        .attr("y", topY + 11)
                        .text(item.value);
                }

                if (item.type === "level") {
                    badgeGroup.append("rect")
                        .attr("class", "card-level-chip")
                        .attr("x", chipX)
                        .attr("y", topY)
                        .attr("width", item.width)
                        .attr("height", chipHeight);

                    badgeGroup.append("text")
                        .attr("class", "card-level-chip-text")
                        .attr("text-anchor", "middle")
                        .attr("x", chipX + (item.width / 2))
                        .attr("y", topY + 11)
                        .text(item.value);
                }

                topY += chipHeight + gap;
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
        setText(el.detailBirthDate, formatDate(person.birthDate));
        setText(el.detailDeathDate, formatDate(person.deathDate));
        setText(el.detailBirthPlace, person.birthPlace || "-");
        setText(el.detailCurrentAddress, person.currentAddress || "-");
        setText(el.detailPhone, person.phone || "-");
        setText(el.detailRank, person.gender === "Male" ? (person.rank ? `行${person.rank}` : "-") : "-");
        setText(el.detailParent, person.parentName || "-");
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
        if (el.detailSpouse) {
            if (person.spouseId && person.spouseName) {
                el.detailSpouse.textContent = person.spouseName;
                el.detailSpouse.onclick = () => openPersonDetail(person.spouseId);
            } else {
                el.detailSpouse.textContent = "-";
                el.detailSpouse.onclick = null;
            }
        }
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

    async function exportTreeAsImage() {
        try {
            const dataUrl = await exportSvgTreeToPngDataUrl(el.treeSvg, 2);
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

    async function exportSvgTreeToPngDataUrl(svgElement, scale = 2) {
        if (!svgElement) {
            throw new Error("SVG not found");
        }

        const clonedSvg = svgElement.cloneNode(true);
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
            const viewBox = svgElement.viewBox?.baseVal;
            const width = Math.max(svgElement.clientWidth || 0, viewBox?.width || 0, 1200);
            const height = Math.max(svgElement.clientHeight || 0, viewBox?.height || 0, 800);
            const canvas = document.createElement("canvas");
            canvas.width = Math.round(width * scale);
            canvas.height = Math.round(height * scale);

            const ctx = canvas.getContext("2d");
            if (!ctx) throw new Error("Canvas context unavailable");

            ctx.fillStyle = "#ffffff";
            ctx.fillRect(0, 0, canvas.width, canvas.height);
            ctx.drawImage(image, 0, 0, canvas.width, canvas.height);
            return canvas.toDataURL("image/png");
        } finally {
            URL.revokeObjectURL(url);
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

    function applyZoom(ratio) {
        if (!state.svg || !state.zoomBehavior) return;
        state.svg.transition().duration(200).call(state.zoomBehavior.scaleBy, ratio);
    }

    function resetZoom() {
        if (!state.svg || !state.zoomBehavior) return;
        const scale = Math.min(2, Math.max(0.5, (state.siteSettings.defaultZoomPercent || 100) / 100));
        state.svg.transition().duration(250).call(state.zoomBehavior.transform, d3.zoomIdentity.translate(120, 80).scale(scale));
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

