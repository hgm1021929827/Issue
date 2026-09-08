import { useEffect, useRef, useState } from "react";
import { useLocation, useNavigate, useParams, useSearchParams } from "react-router-dom";
import { CircleCheck, ClipboardList, FilePlus2, FileSpreadsheet, FolderKanban, ListTodo, Sparkles, Trash2 } from "lucide-react";
import { api } from "../api.js";
import AppDialog from "../components/AppDialog.jsx";
import AppSelect from "../components/AppSelect.jsx";
import AppDateField from "../components/AppDateField.jsx";
import PageTitle from "../components/PageTitle.jsx";
import VendorSelect from "../components/VendorSelect.jsx";
import TrackTodoList from "../components/TrackTodoList.jsx";
import WorkHourTable from "../components/WorkHourTable.jsx";
import ImportExcelDialog from "../components/ImportExcelDialog.jsx";
import { peekPendingImport, takePendingImport } from "../importSession.js";

const emptyProject = {
  code: "",
  name: "",
  description: "",
  majorCategoryId: "",
  subCategoryId: "",
  startDate: "",
  dueDate: "",
  clientCompanyId: "",
  ownerMemberId: "",
  createdAt: "",
  updatedAt: "",
  itemCount: 0
};

const emptyItem = {
  seqNo: "",
  title: "",
  content: "",
  majorCategoryId: "",
  subCategoryId: "",
  startDate: "",
  dueDate: "",
  handlerName: ""
};

function formatDateTime(value) {
  if (!value) return "—";
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return String(value);
  const pad = (n) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

function DoneMark({ done }) {
  return (
    <span className={`item-done-icon${done ? " is-on" : ""}`} title={done ? "已完成" : undefined}>
      {done ? <CircleCheck size={18} strokeWidth={1.9} aria-hidden="true" /> : null}
    </span>
  );
}

function defaultMajorId(majors) {
  return majors.find((m) => m.name === "議題分類")?.id
    || majors.find((m) => m.name === "預約")?.id
    || majors[0]?.id;
}

export default function ProjectPage() {
  const { id } = useParams();
  const isNew = !id;
  const navigate = useNavigate();
  const location = useLocation();
  const [searchParams] = useSearchParams();
  const highlightId = searchParams.get("item");
  const highlightWorkItemId = searchParams.get("workItem");
  const highlightTrackId = searchParams.get("trackTodo");
  const itemRefs = useRef({});
  const workItemRefs = useRef({});

  const [majors, setMajors] = useState([]);
  const [subs, setSubs] = useState([]);
  const [itemSubs, setItemSubs] = useState([]);
  const [members, setMembers] = useState([]);
  const [form, setForm] = useState(emptyProject);
  const [items, setItems] = useState([]);
  const [tracks, setTracks] = useState([]);
  const [itemTracks, setItemTracks] = useState([]);
  const [tracksReady, setTracksReady] = useState(false);
  const [itemTracksReady, setItemTracksReady] = useState(false);
  const [itemForm, setItemForm] = useState(emptyItem);
  const [editingId, setEditingId] = useState(null);
  const [showItemForm, setShowItemForm] = useState(false);
  const [error, setError] = useState("");
  const [itemError, setItemError] = useState("");
  const [saving, setSaving] = useState(false);
  const [askDelete, setAskDelete] = useState(false);
  const [askDeleteItem, setAskDeleteItem] = useState(null);
  const [askDeleteWorkItem, setAskDeleteWorkItem] = useState(null);
  const [askImport, setAskImport] = useState(false);
  const [workItems, setWorkItems] = useState([]);
  const [selectedWorkItem, setSelectedWorkItem] = useState(null);
  const [workItemHours, setWorkItemHours] = useState([]);
  const [workItemTracks, setWorkItemTracks] = useState([]);
  const [workItemTracksReady, setWorkItemTracksReady] = useState(false);
  const [itemHours, setItemHours] = useState([]);
  const [tab, setTab] = useState(() => {
    if (searchParams.get("item") || searchParams.get("tab") === "items") return "items";
    if (searchParams.get("tab") === "content") return "content";
    return "workItems";
  });

  const loadSubs = async (majorId, keepSub, setter) => {
    const list = await api.subCategories(majorId);
    setter(list);
    if (keepSub && list.some((s) => String(s.id) === String(keepSub))) {
      return keepSub;
    }
    return "";
  };

  const applyProject = (project) => {
    setForm({
      code: project.code || "",
      name: project.name || "",
      description: project.description || "",
      majorCategoryId: project.majorCategoryId,
      subCategoryId: project.subCategoryId || "",
      startDate: project.startDate || "",
      dueDate: project.dueDate || "",
      clientCompanyId: project.clientCompanyId || "",
      clientCompanyName: project.clientCompanyName || "",
      ownerMemberId: project.ownerMemberId || "",
      createdAt: project.createdAt || "",
      updatedAt: project.updatedAt || "",
      itemCount: project.itemCount || project.items?.length || 0,
      workItemCount: project.workItemCount || 0
    });
    setItems(project.items || []);
  };

  useEffect(() => {
    setTracksReady(false);
    (async () => {
      try {
        const [majorList, memberList] = await Promise.all([api.majorCategories(), api.companyMembers()]);
        setMajors(majorList);
        setMembers(memberList);
        if (isNew) {
          const majorId = defaultMajorId(majorList);
          await loadSubs(majorId, "", setSubs);
          const pending = location.state || peekPendingImport() || {};
          setForm({
            ...emptyProject,
            majorCategoryId: majorId,
            code: pending.projectCode || "",
            name: pending.suggestedName || ""
          });
          return;
        }
        const [project, trackList, workItemList] = await Promise.all([
          api.project(id),
          api.projectTrackTodos(id),
          api.projectWorkItems(id)
        ]);
        await loadSubs(project.majorCategoryId, project.subCategoryId, setSubs);
        applyProject(project);
        setTracks(trackList);
        setWorkItems(workItemList);
        setTracksReady(true);
      } catch (err) {
        setError(err.message);
        if (!isNew) navigate("/projects");
      }
    })();
  }, [id]);

  useEffect(() => {
    if (searchParams.get("item") || searchParams.get("tab") === "items") setTab("items");
    else if (searchParams.get("tab") === "content") setTab("content");
    else setTab("workItems");
  }, [id]);

  useEffect(() => {
    if (highlightId) setTab("items");
  }, [highlightId]);

  useEffect(() => {
    if (highlightWorkItemId) setTab("workItems");
  }, [highlightWorkItemId]);

  useEffect(() => {
    if (!highlightId || items.length === 0) return;
    const found = items.find((x) => String(x.id) === String(highlightId));
    if (!found) {
      setError("找不到該專案議題");
      return;
    }
    setTab("items");
    openEdit(found);
    const timer = window.setTimeout(() => {
      itemRefs.current[found.id]?.scrollIntoView({ behavior: "smooth", block: "center" });
    }, 80);
    return () => window.clearTimeout(timer);
  }, [highlightId, items]);

  useEffect(() => {
    if (!editingId || isNew) {
      setItemTracks([]);
      setItemTracksReady(false);
      return;
    }
    setItemTracksReady(false);
    api.projectItemTrackTodos(id, editingId)
      .then((list) => {
        setItemTracks(list);
        setItemTracksReady(true);
      })
      .catch((err) => setItemError(err.message));
    api.itemHours(id, editingId)
      .then(setItemHours)
      .catch((err) => setItemError(err.message));
  }, [editingId, id, isNew]);

  useEffect(() => {
    if (!highlightWorkItemId || workItems.length === 0) return;
    const found = workItems.find((x) => String(x.id) === String(highlightWorkItemId));
    if (!found) {
      setError("找不到該工作項次");
      return;
    }
    setTab("workItems");
    openWorkItem(found);
    const timer = window.setTimeout(() => {
      workItemRefs.current[found.id]?.scrollIntoView({ behavior: "smooth", block: "center" });
    }, 80);
    return () => window.clearTimeout(timer);
  }, [highlightWorkItemId, workItems]);

  const openWorkItem = async (item) => {
    setSelectedWorkItem(item);
    setWorkItemTracksReady(false);
    try {
      const [detail, hourList, trackList] = await Promise.all([
        api.projectWorkItem(id, item.id),
        api.workItemHours(id, item.id),
        api.workItemTrackTodos(id, item.id)
      ]);
      setSelectedWorkItem(detail);
      setWorkItemHours(hourList);
      setWorkItemTracks(trackList);
      setWorkItemTracksReady(true);
    } catch (err) {
      setError(err.message);
    }
  };

  const projectPayload = () => ({
    code: form.code,
    name: form.name,
    description: form.description,
    majorCategoryId: Number(form.majorCategoryId),
    subCategoryId: form.subCategoryId === "" ? null : Number(form.subCategoryId),
    startDate: form.startDate || null,
    dueDate: form.dueDate || null,
    clientCompanyId: form.clientCompanyId === "" ? null : Number(form.clientCompanyId),
    ownerMemberId: form.ownerMemberId === "" ? null : Number(form.ownerMemberId)
  });

  const itemPayload = () => ({
    seqNo: itemForm.seqNo,
    title: itemForm.title,
    content: itemForm.content,
    majorCategoryId: Number(itemForm.majorCategoryId),
    subCategoryId: itemForm.subCategoryId === "" ? null : Number(itemForm.subCategoryId),
    startDate: editingId ? (items.find((x) => x.id === editingId)?.startDate || null) : null,
    dueDate: itemForm.dueDate || null,
    handlerName: itemForm.handlerName || ""
  });

  const save = async (event) => {
    event.preventDefault();
    if (form.clientCompanyId === "" || form.clientCompanyId == null) {
      setError("請選擇客戶公司");
      return;
    }
    setSaving(true);
    setError("");
    try {
      if (isNew) {
        const created = await api.createProject(projectPayload());
        const pending = takePendingImport() || location.state;
        if (pending?.file && pending?.memberId) {
          const result = await api.importProjectExcel(created.id, pending.file, pending.memberId);
          navigate(`/projects/${created.id}/import-result`, { state: { result } });
          return;
        }
        navigate(`/projects/${created.id}`);
      } else {
        const project = await api.updateProject(id, projectPayload());
        applyProject(project);
      }
    } catch (err) {
      setError(err.message);
    } finally {
      setSaving(false);
    }
  };

  const closeItemForm = () => {
    setShowItemForm(false);
    setEditingId(null);
    setItemError("");
  };

  const openCreateItem = async () => {
    const majorId = defaultMajorId(majors) || form.majorCategoryId;
    await loadSubs(majorId, "", setItemSubs);
    setEditingId(null);
    setItemError("");
    setItemForm({ ...emptyItem, majorCategoryId: majorId });
    setShowItemForm(true);
  };

  const openEdit = async (item) => {
    await loadSubs(item.majorCategoryId, item.subCategoryId, setItemSubs);
    setEditingId(item.id);
    setItemError("");
    setItemForm({
      seqNo: item.seqNo,
      title: item.title,
      content: item.content || "",
      majorCategoryId: item.majorCategoryId,
      subCategoryId: item.subCategoryId || "",
      startDate: item.startDate || "",
      dueDate: item.dueDate || "",
      handlerName: item.handlerName || ""
    });
    setShowItemForm(true);
  };

  const saveItem = async (event) => {
    event?.preventDefault();
    setItemError("");
    try {
      const prevIds = new Set(items.map((x) => x.id));
      const list = editingId
        ? await api.updateProjectItem(id, editingId, itemPayload())
        : await api.createProjectItem(id, itemPayload());
      setItems(list);
      setForm((prev) => ({ ...prev, itemCount: list.length }));
      const keepId = editingId || list.find((x) => !prevIds.has(x.id))?.id;
      const kept = list.find((x) => x.id === keepId);
      if (kept) await openEdit(kept);
      else closeItemForm();
    } catch (err) {
      setItemError(err.message);
    }
  };

  const selectedMajorName = majors.find((m) => String(m.id) === String(form.majorCategoryId))?.name || "請選擇";
  const itemMajorName = majors.find((m) => String(m.id) === String(itemForm.majorCategoryId))?.name || "請選擇";

  return (
    <div>
      {error && <p className="banner error">{error}</p>}
      {!isNew && (
        <>
          <PageTitle icon={FolderKanban}>
            {[form.code || id, form.name].filter(Boolean).join(" ")}
          </PageTitle>
          <div className="filters member-tabs" role="tablist" aria-label="專案頁面">
            <button
              type="button"
              role="tab"
              className={tab === "content" ? "chip active" : "chip"}
              aria-selected={tab === "content"}
              onClick={() => setTab("content")}
            >
              專案內容
            </button>
            <button
              type="button"
              role="tab"
              className={tab === "workItems" ? "chip active" : "chip"}
              aria-selected={tab === "workItems"}
              onClick={() => setTab("workItems")}
            >
              工作項次（{workItems.length}）
            </button>
            <button
              type="button"
              role="tab"
              className={tab === "items" ? "chip active" : "chip"}
              aria-selected={tab === "items"}
              onClick={() => setTab("items")}
            >
              專案議題（{items.length}）
            </button>
            <button type="button" className="chip" onClick={() => { setError(""); setAskImport(true); }}>
              <FileSpreadsheet size={14} strokeWidth={1.75} aria-hidden="true" />
              匯入 Excel
            </button>
          </div>
        </>
      )}
      {(isNew || tab === "content") && (
      <form className="card form" onSubmit={save}>
        {isNew && (
          <PageTitle icon={FilePlus2}>新增專案</PageTitle>
        )}
        <div className={`two-col${isNew ? "" : " issue-meta-fields"}`}>
          <label>
            專案代號
            <input
              required
              maxLength={50}
              value={form.code}
              onChange={(e) => setForm({ ...form, code: e.target.value })}
            />
          </label>
          {!isNew && (
            <>
              <label>
                建立時間
                <input readOnly value={formatDateTime(form.createdAt)} />
              </label>
              <label>
                更新時間
                <input readOnly value={formatDateTime(form.updatedAt)} />
              </label>
            </>
          )}
        </div>
        <label>
          專案名稱
          <input required value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} />
        </label>
        <label>
          工作說明
          <textarea rows={4} value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} />
        </label>
        <div className="two-col">
          <label>
            大分類
            <AppSelect
              value={form.majorCategoryId}
              options={majors.map((m) => ({ value: m.id, label: m.name }))}
              onChange={async (majorCategoryId) => {
                const sub = await loadSubs(majorCategoryId, form.subCategoryId, setSubs);
                setForm({ ...form, majorCategoryId, subCategoryId: sub });
              }}
            />
          </label>
          <label>
            小分類
            <AppSelect
              colored
              value={form.subCategoryId}
              placeholder={selectedMajorName}
              options={[
                { value: "", label: selectedMajorName },
                ...subs.map((s) => ({ value: s.id, label: s.name, color: s.color }))
              ]}
              onChange={(subCategoryId) => setForm({ ...form, subCategoryId })}
            />
          </label>
        </div>
        <div className="two-col">
          <label>
            預計開始日
            <AppDateField value={form.startDate} onChange={(startDate) => setForm({ ...form, startDate })} />
          </label>
          <label>
            預計完成日
            <AppDateField value={form.dueDate} onChange={(dueDate) => setForm({ ...form, dueDate })} />
          </label>
        </div>
        <div className="two-col">
          <label>
            客戶公司
            <VendorSelect
              value={form.clientCompanyId}
              onChange={(clientCompanyId) => setForm({ ...form, clientCompanyId })}
            />
          </label>
          <label>
            負責人員
            <AppSelect
              value={form.ownerMemberId}
              placeholder="未指定"
              options={[
                { value: "", label: "未指定" },
                ...members.map((m) => ({ value: m.id, label: `${m.name}（${m.englishName}）` }))
              ]}
              onChange={(ownerMemberId) => setForm({ ...form, ownerMemberId })}
            />
          </label>
        </div>
        <div className="btn-row">
          <button className="btn" disabled={saving} type="submit">
            {saving ? "儲存中…" : "儲存"}
          </button>
          {!isNew && (
            <button className="btn danger" type="button" onClick={() => setAskDelete(true)}>
              刪除專案
            </button>
          )}
        </div>
      </form>
      )}

      {!isNew && tab === "content" && (
        <TrackTodoList
          items={tracks}
          onChange={setTracks}
          onError={setError}
          vendorCompanyId={form.clientCompanyId === "" ? null : form.clientCompanyId}
          create={(payload) => api.createProjectTrackTodo(id, payload)}
          highlightId={!highlightId ? highlightTrackId : null}
          ready={tracksReady}
        />
      )}

      {!isNew && tab === "items" && (
        <section className="card project-items">
          <div className="row-between">
            <PageTitle icon={ClipboardList} as="h2">
              專案議題
            </PageTitle>
            <button className="btn" type="button" onClick={openCreateItem}>
              新增專案議題
            </button>
          </div>
          <div className="project-item-workspace">
            <div className="project-item-list">
              {items.length === 0 && <p className="muted">尚無專案議題。</p>}
              <ul className="issue-list">
                {items.map((item) => (
                  <li
                    key={item.id}
                    ref={(node) => {
                      itemRefs.current[item.id] = node;
                    }}
                    className={`issue-row${String(highlightId) === String(item.id) || editingId === item.id ? " is-highlight" : ""}${item.isCompleted ? " is-done" : ""}`}
                  >
                    <button
                      type="button"
                      className="project-item-btn"
                      style={{ "--accent-color": item.subCategoryColor || item.majorCategoryColor }}
                      onClick={() => openEdit(item)}
                    >
                      <DoneMark done={item.isCompleted} />
                      <span className="tag">{item.subCategoryName || item.majorCategoryName}</span>
                      <strong>
                        <span className="issue-id">{item.seqNo}</span>
                        {item.title}
                        {item.missingKept ? <span className="tag is-kept">檔中沒有</span> : null}
                      </strong>
                      <em className="due">{item.dueDate || "無預計完成日"}</em>
                    </button>
                    <button
                      className="todo-icon-btn danger"
                      type="button"
                      title="刪除"
                      aria-label="刪除"
                      onClick={() => setAskDeleteItem(item)}
                    >
                      <Trash2 size={16} strokeWidth={1.75} aria-hidden="true" />
                    </button>
                  </li>
                ))}
              </ul>
            </div>
            <div className="project-item-detail">
              {!showItemForm && (
                <div className="empty">
                  <Sparkles className="empty-icon" size={22} strokeWidth={1.75} aria-hidden="true" />
                  <p>請選擇左側專案議題，或按新增。</p>
                </div>
              )}
              {showItemForm && (
                <>
                  <form className="form" onSubmit={saveItem}>
                    <PageTitle as="h3" icon={ClipboardList}>
                      {editingId ? "編輯專案議題" : "新增專案議題"}
                    </PageTitle>
                    {itemError && <p className="banner error">{itemError}</p>}
                    <label>
                      項次
                      <input
                        required
                        maxLength={50}
                        readOnly={Boolean(editingId)}
                        value={itemForm.seqNo}
                        onChange={(e) => setItemForm({ ...itemForm, seqNo: e.target.value })}
                      />
                    </label>
                    <label>
                      標題
                      <textarea
                        required
                        rows={3}
                        maxLength={200}
                        value={itemForm.title}
                        onChange={(e) => setItemForm({ ...itemForm, title: e.target.value })}
                      />
                    </label>
                    <label>
                      內容
                      <textarea rows={3} value={itemForm.content} onChange={(e) => setItemForm({ ...itemForm, content: e.target.value })} />
                    </label>
                    <div className="two-col">
                      <label>
                        大分類
                        <AppSelect
                          value={itemForm.majorCategoryId}
                          options={majors.map((m) => ({ value: m.id, label: m.name }))}
                          onChange={async (majorCategoryId) => {
                            const sub = await loadSubs(majorCategoryId, itemForm.subCategoryId, setItemSubs);
                            setItemForm({ ...itemForm, majorCategoryId, subCategoryId: sub });
                          }}
                        />
                      </label>
                      <label>
                        小分類
                        <AppSelect
                          colored
                          value={itemForm.subCategoryId}
                          placeholder={itemMajorName}
                          options={[
                            { value: "", label: itemMajorName },
                            ...itemSubs.map((s) => ({ value: s.id, label: s.name, color: s.color }))
                          ]}
                          onChange={(subCategoryId) => setItemForm({ ...itemForm, subCategoryId })}
                        />
                      </label>
                    </div>
                    <label>
                      預計完成日
                      <AppDateField value={itemForm.dueDate} onChange={(dueDate) => setItemForm({ ...itemForm, dueDate })} />
                    </label>
                    <label>
                      處理人員
                      <input
                        maxLength={50}
                        value={itemForm.handlerName}
                        onChange={(e) => setItemForm({ ...itemForm, handlerName: e.target.value })}
                      />
                    </label>
                    {editingId && (
                      <p className="muted">
                        實際開始 {items.find((x) => x.id === editingId)?.actualStartDate || "—"}
                        ／實際結束 {items.find((x) => x.id === editingId)?.actualEndDate || "—"}
                        ／合計 {Number(items.find((x) => x.id === editingId)?.hoursTotal || 0).toFixed(2)}
                      </p>
                    )}
                    <div className="btn-row">
                      <button className="btn" type="submit">
                        儲存專案議題
                      </button>
                      {editingId && (
                        <button
                          className="btn"
                          type="button"
                          onClick={async () => {
                            try {
                              const updated = await api.completeProjectItem(
                                id,
                                editingId,
                                !items.find((x) => x.id === editingId)?.isCompleted
                              );
                              const list = await api.projectItems(id);
                              setItems(list);
                              const kept = list.find((x) => x.id === updated.id);
                              if (kept) await openEdit(kept);
                            } catch (err) {
                              setItemError(err.message);
                            }
                          }}
                        >
                          {items.find((x) => x.id === editingId)?.isCompleted ? "取消完成" : "標記完成"}
                        </button>
                      )}
                      <button className="btn" type="button" onClick={closeItemForm}>
                        取消
                      </button>
                    </div>
                  </form>
                  {editingId && (
                    <WorkHourTable
                      hours={itemHours}
                      hoursTotal={items.find((x) => x.id === editingId)?.hoursTotal}
                      onError={setItemError}
                      onCreate={async (payload) => {
                        const list = await api.createItemHour(id, editingId, payload);
                        setItemHours(list);
                        setItems(await api.projectItems(id));
                      }}
                      onUpdate={async (hourId, payload) => {
                        const list = await api.updateItemHour(id, editingId, hourId, payload);
                        setItemHours(list);
                        setItems(await api.projectItems(id));
                      }}
                      onDelete={async (hourId) => {
                        const list = await api.deleteItemHour(id, editingId, hourId);
                        setItemHours(list);
                        setItems(await api.projectItems(id));
                      }}
                    />
                  )}
                  {editingId && (
                    <TrackTodoList
                      compact
                      items={itemTracks}
                      onChange={setItemTracks}
                      onError={setItemError}
                      vendorCompanyId={form.clientCompanyId === "" ? null : form.clientCompanyId}
                      create={(payload) => api.createProjectItemTrackTodo(id, editingId, payload)}
                      highlightId={highlightTrackId}
                      ready={itemTracksReady}
                    />
                  )}
                </>
              )}
            </div>
          </div>
        </section>
      )}

      {!isNew && tab === "workItems" && (
        <section className="card project-items">
          <div className="row-between">
            <PageTitle icon={ListTodo} as="h2">
              工作項次
            </PageTitle>
          </div>
          <div className="project-item-workspace">
            <div className="project-item-list">
              {workItems.length === 0 && (
                <p className="muted">尚無工作項次。請從專案清單或本頁「匯入 Excel」匯入。</p>
              )}
              <ul className="issue-list">
                {workItems.map((item) => (
                  <li
                    key={item.id}
                    ref={(node) => {
                      workItemRefs.current[item.id] = node;
                    }}
                    className={`issue-row${String(highlightWorkItemId) === String(item.id) || selectedWorkItem?.id === item.id ? " is-highlight" : ""}${item.isCompleted ? " is-done" : ""}`}
                  >
                    <button
                      type="button"
                      className="project-item-btn"
                      onClick={() => openWorkItem(item)}
                    >
                      <DoneMark done={item.isCompleted} />
                      <strong>
                        <span className="issue-id">{item.workItemCode}</span>
                        {item.title || "（無說明）"}
                        {item.missingKept ? <span className="tag is-kept">檔中沒有</span> : null}
                      </strong>
                      <em className="due">{item.ownerName || "未指定負責人"}</em>
                      <em className="due">{item.dueDate || "無預計完成日"}</em>
                    </button>
                    <button
                      className="todo-icon-btn danger"
                      type="button"
                      title="刪除"
                      aria-label="刪除"
                      onClick={() => setAskDeleteWorkItem(item)}
                    >
                      <Trash2 size={16} strokeWidth={1.75} aria-hidden="true" />
                    </button>
                  </li>
                ))}
              </ul>
            </div>
            <div className="project-item-detail">
              {!selectedWorkItem && (
                <div className="empty">
                  <Sparkles className="empty-icon" size={22} strokeWidth={1.75} aria-hidden="true" />
                  <p>請選擇左側工作項次。</p>
                </div>
              )}
              {selectedWorkItem && (
                <>
                  <PageTitle as="h3" icon={ListTodo}>
                    {selectedWorkItem.workItemCode}
                  </PageTitle>
                  <dl className="readonly-fields">
                    <div><dt>工作代號</dt><dd>{selectedWorkItem.workItemCode}</dd></div>
                    <div><dt>工作說明</dt><dd>{selectedWorkItem.title || "—"}</dd></div>
                    <div><dt>計畫人天</dt><dd>{selectedWorkItem.plannedDays ?? "—"}</dd></div>
                    <div><dt>負責人員</dt><dd>{selectedWorkItem.ownerName || "—"}</dd></div>
                    <div><dt>預計開始日</dt><dd>{selectedWorkItem.startDate || "—"}</dd></div>
                    <div><dt>預計完成日</dt><dd>{selectedWorkItem.dueDate || "—"}</dd></div>
                    <div><dt>實際開始日</dt><dd>{selectedWorkItem.actualStartDate || "—"}</dd></div>
                    <div><dt>實際結束日</dt><dd>{selectedWorkItem.actualEndDate || "—"}</dd></div>
                    <div><dt>目前工時</dt><dd>{Number(selectedWorkItem.hoursTotal || 0).toFixed(2)}</dd></div>
                    <div className="readonly-span">
                      <dt>備註</dt>
                      <dd className="work-item-remark">{selectedWorkItem.remark || "—"}</dd>
                    </div>
                  </dl>
                  <div className="btn-row">
                    <button
                      className="btn"
                      type="button"
                      onClick={async () => {
                        try {
                          const updated = await api.completeProjectWorkItem(
                            id,
                            selectedWorkItem.id,
                            !selectedWorkItem.isCompleted
                          );
                          setSelectedWorkItem(updated);
                          setWorkItems(await api.projectWorkItems(id));
                        } catch (err) {
                          setError(err.message);
                        }
                      }}
                    >
                      {selectedWorkItem.isCompleted ? "取消完成" : "標記完成"}
                    </button>
                  </div>
                  <WorkHourTable
                    hours={workItemHours}
                    hoursTotal={selectedWorkItem.hoursTotal}
                    onError={setError}
                    onCreate={async (payload) => {
                      const list = await api.createWorkItemHour(id, selectedWorkItem.id, payload);
                      setWorkItemHours(list);
                      await openWorkItem(selectedWorkItem);
                      setWorkItems(await api.projectWorkItems(id));
                    }}
                    onUpdate={async (hourId, payload) => {
                      const list = await api.updateWorkItemHour(id, selectedWorkItem.id, hourId, payload);
                      setWorkItemHours(list);
                      await openWorkItem(selectedWorkItem);
                      setWorkItems(await api.projectWorkItems(id));
                    }}
                    onDelete={async (hourId) => {
                      const list = await api.deleteWorkItemHour(id, selectedWorkItem.id, hourId);
                      setWorkItemHours(list);
                      await openWorkItem(selectedWorkItem);
                      setWorkItems(await api.projectWorkItems(id));
                    }}
                  />
                  <TrackTodoList
                    compact
                    items={workItemTracks}
                    onChange={setWorkItemTracks}
                    onError={setError}
                    vendorCompanyId={form.clientCompanyId === "" ? null : form.clientCompanyId}
                    create={(payload) => api.createWorkItemTrackTodo(id, selectedWorkItem.id, payload)}
                    highlightId={highlightTrackId}
                    ready={workItemTracksReady}
                  />
                </>
              )}
            </div>
          </div>
        </section>
      )}

      <ImportExcelDialog
        open={askImport}
        onClose={() => setAskImport(false)}
        onError={setError}
        onNeedProject={(session) => navigate("/projects/new", { state: session })}
        onReady={async (projectId, session) => {
          try {
            const result = await api.importProjectExcel(projectId, session.file, session.memberId);
            navigate(`/projects/${projectId}/import-result`, { state: { result } });
          } catch (err) {
            setError(err.message);
          }
        }}
      />

      <AppDialog
        open={askDelete}
        title="刪除專案"
        message={`確定刪除專案「${form.code || form.name}」？將一併刪除 ${form.itemCount} 筆專案議題、${form.workItemCount || workItems.length} 筆工作項次${tracks.length ? `、${tracks.length} 筆專案上的需要追蹤的 TODO` : ""}。`}
        confirmLabel="刪除"
        danger
        onCancel={() => setAskDelete(false)}
        onSubmit={async () => {
          try {
            await api.deleteProject(id);
            navigate("/projects");
          } catch (err) {
            setError(err.message);
            setAskDelete(false);
          }
        }}
      />
      <AppDialog
        open={Boolean(askDeleteItem)}
        title="刪除專案議題"
        message={askDeleteItem ? `確定刪除項次「${askDeleteItem.seqNo}」？${itemTracks.length && editingId === askDeleteItem.id ? `將一併刪除 ${itemTracks.length} 筆需要追蹤的 TODO。` : ""}` : ""}
        confirmLabel="刪除"
        danger
        onCancel={() => setAskDeleteItem(null)}
        onSubmit={async () => {
          try {
            const list = await api.deleteProjectItem(id, askDeleteItem.id);
            setItems(list);
            setForm((prev) => ({ ...prev, itemCount: list.length }));
            if (editingId === askDeleteItem.id) {
              closeItemForm();
            }
            setAskDeleteItem(null);
          } catch (err) {
            setError(err.message);
            setAskDeleteItem(null);
          }
        }}
      />
      <AppDialog
        open={Boolean(askDeleteWorkItem)}
        title="刪除工作項次"
        message={askDeleteWorkItem ? `確定刪除工作項次「${askDeleteWorkItem.workItemCode}」？將一併刪除其工時與追蹤 TODO。` : ""}
        confirmLabel="刪除"
        danger
        onCancel={() => setAskDeleteWorkItem(null)}
        onSubmit={async () => {
          try {
            await api.deleteProjectWorkItem(id, askDeleteWorkItem.id);
            const list = await api.projectWorkItems(id);
            setWorkItems(list);
            if (selectedWorkItem?.id === askDeleteWorkItem.id) {
              setSelectedWorkItem(null);
              setWorkItemHours([]);
              setWorkItemTracks([]);
            }
            setAskDeleteWorkItem(null);
          } catch (err) {
            setError(err.message);
            setAskDeleteWorkItem(null);
          }
        }}
      />
    </div>
  );
}
