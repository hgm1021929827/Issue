import { useEffect, useRef, useState } from "react";
import { Link, useNavigate, useParams, useSearchParams } from "react-router-dom";
import { ArrowLeft, CalendarClock, Trash2 } from "lucide-react";
import { api } from "../api.js";
import AppDateField from "../components/AppDateField.jsx";
import AppSelect from "../components/AppSelect.jsx";
import PageTitle from "../components/PageTitle.jsx";
import VendorSelect from "../components/VendorSelect.jsx";
import AppDialog from "../components/AppDialog.jsx";
import AppointmentWorkPreview from "../components/AppointmentWorkPreview.jsx";

function flattenTodos(nodes, prefix = "") {
  const rows = [];
  for (const node of nodes || []) {
    const label = prefix ? `${prefix} / ${node.title}` : node.title;
    rows.push({ id: node.id, title: label });
    rows.push(...flattenTodos(node.children, label));
  }
  return rows;
}

function itemKey(item) {
  if (item.issueId && !item.todoId) return `issue:${item.issueId}`;
  if (item.projectId && !item.todoId) return `project:${item.projectId}`;
  if (item.todoId) return `todo:${item.todoId}`;
  return "";
}

const CHANNEL_TYPES = [
  { value: "phone", label: "市話" },
  { value: "mobile", label: "手機" },
  { value: "email", label: "Email" },
  { value: "teams", label: "Teams" },
  { value: "other", label: "其他" }
];

function channelOptionLabel(channel) {
  const type = CHANNEL_TYPES.find((x) => x.value === channel.type)?.label || channel.type || "聯絡";
  return channel.value ? `${type} ${channel.value}` : type;
}

export default function AppointmentPage() {
  const { id } = useParams();
  const isNew = !id;
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const sourceIssueId = searchParams.get("issue");
  const sourceProjectId = searchParams.get("project");
  const [error, setError] = useState("");
  const [saving, setSaving] = useState(false);
  const [statusMeta, setStatusMeta] = useState({ majorCategoryId: null, name: "", color: "", subCategories: [] });
  const [companyId, setCompanyId] = useState("");
  const [companyLocked, setCompanyLocked] = useState(false);
  const [contactId, setContactId] = useState("");
  const [channelId, setChannelId] = useState("");
  const [contacts, setContacts] = useState([]);
  const [statusId, setStatusId] = useState("");
  const [date, setDate] = useState("");
  const [items, setItems] = useState([]);
  const [issues, setIssues] = useState([]);
  const [projects, setProjects] = useState([]);
  const [pickKind, setPickKind] = useState("issue");
  const [pickWorkKind, setPickWorkKind] = useState("issue");
  const [pickIssueId, setPickIssueId] = useState("");
  const [pickProjectId, setPickProjectId] = useState("");
  const [pickTodoId, setPickTodoId] = useState("");
  const [todoOptions, setTodoOptions] = useState([]);
  const [askDelete, setAskDelete] = useState(false);
  const [previewItem, setPreviewItem] = useState(null);
  const [futures, setFutures] = useState([]);
  const [mergeOpen, setMergeOpen] = useState(false);
  const [mergeChoice, setMergeChoice] = useState("");
  const pendingRef = useRef(null);
  const closeAfterRef = useRef(false);
  const mergeDialogRef = useRef(null);

  const statusOptions = statusMeta.subCategories || [];
  const selectedChannels = contacts.find((c) => String(c.id) === String(contactId))?.channels || [];
  const companyIssues = companyId
    ? issues.filter((x) => String(x.clientCompanyId) === String(companyId))
    : [];
  const companyProjects = companyId
    ? projects.filter((x) => String(x.clientCompanyId) === String(companyId))
    : [];

  const loadContacts = async (cid) => {
    if (!cid) {
      setContacts([]);
      return;
    }
    setContacts(await api.clientContacts(cid));
  };

  useEffect(() => {
    (async () => {
      try {
        const [status, issueList, projectList] = await Promise.all([
          api.appointmentStatus(),
          api.issues(),
          api.projects()
        ]);
        setStatusMeta(status);
        setIssues(issueList);
        setProjects(projectList);
        if (isNew) {
          const preset = status.subCategories?.find((s) => s.name === "已發信") || status.subCategories?.[0];
          if (preset) setStatusId(String(preset.id));
          if (sourceIssueId) {
            const issue = await api.issue(sourceIssueId);
            if (!issue.clientCompanyId) {
              setError("請先設定廠商");
              return;
            }
            setCompanyId(String(issue.clientCompanyId));
            setCompanyLocked(true);
            await loadContacts(issue.clientCompanyId);
            setItems([{
              kind: "issue",
              issueId: issue.id,
              title: issue.issueNo ? `#${issue.issueNo} ${issue.title}` : issue.title,
              workLabel: issue.title
            }]);
          } else if (sourceProjectId) {
            const project = await api.project(sourceProjectId);
            if (!project.clientCompanyId) {
              setError("請先設定廠商");
              return;
            }
            setCompanyId(String(project.clientCompanyId));
            setCompanyLocked(true);
            await loadContacts(project.clientCompanyId);
            setItems([{
              kind: "project",
              projectId: project.id,
              title: `${project.code} ${project.name}`.trim(),
              workLabel: project.name
            }]);
          }
          return;
        }
        const row = await api.appointment(id);
        setCompanyId(String(row.clientCompanyId));
        setContactId(String(row.clientContactId));
        setChannelId(row.contactChannelId ? String(row.contactChannelId) : "");
        setStatusId(row.subCategoryId ? String(row.subCategoryId) : "");
        setDate(row.appointmentDate || "");
        setItems(row.items);
        await loadContacts(row.clientCompanyId);
      } catch (err) {
        setError(err.message);
      }
    })();
  }, [id]);

  useEffect(() => {
    const node = mergeDialogRef.current;
    if (!node) return;
    if (mergeOpen) {
      if (!node.open) node.showModal();
    } else if (node.open) {
      node.close();
    }
  }, [mergeOpen]);

  const changeCompany = async (nextId) => {
    setCompanyId(nextId);
    setContactId("");
    setChannelId("");
    setPickIssueId("");
    setPickProjectId("");
    setPickTodoId("");
    setTodoOptions([]);
    await loadContacts(nextId);
  };

  const changeContact = (nextId) => {
    setContactId(nextId);
    const contact = contacts.find((c) => String(c.id) === String(nextId));
    const channels = contact?.channels || [];
    setChannelId(channels.length === 1 ? String(channels[0].id) : "");
  };

  const loadTodoOptions = async (workKind, workId) => {
    if (!workId) {
      setTodoOptions([]);
      return;
    }
    const tree = workKind === "issue"
      ? await api.todos(workId)
      : await api.projectTodos(workId);
    setTodoOptions(flattenTodos(tree));
  };

  const headerPayload = () => ({
    clientCompanyId: Number(companyId),
    clientContactId: Number(contactId),
    contactChannelId: channelId === "" ? null : Number(channelId),
    subCategoryId: statusId === "" ? null : Number(statusId),
    appointmentDate: date || null
  });

  const itemsPayload = (list) => list.map((item) => ({
    issueId: item.kind === "issue" ? item.issueId : undefined,
    projectId: item.kind === "project" ? item.projectId : undefined,
    todoId: item.kind === "todo" ? item.todoId : undefined
  }));

  const validate = () => {
    if (!statusMeta.majorCategoryId && statusId) {
      setError("找不到大分類「預約」");
      return false;
    }
    if (!statusMeta.majorCategoryId) {
      setError("找不到大分類「預約」");
      return false;
    }
    if (!companyId) {
      setError("請選擇客戶公司");
      return false;
    }
    if (!contactId) {
      setError("請選擇客戶窗口");
      return false;
    }
    return true;
  };

  const createNew = async () => {
    const created = await api.createAppointment({
      ...headerPayload(),
      items: itemsPayload(items)
    });
    if (closeAfterRef.current) {
      navigate("/appointments");
      return;
    }
    navigate(`/appointments/${created.id}`);
  };

  const saveEdit = async () => {
    await api.updateAppointment(id, headerPayload());
    if (closeAfterRef.current) {
      navigate("/appointments");
      return;
    }
    const row = await api.appointment(id);
    setItems(row.items);
    setDate(row.appointmentDate || "");
  };

  const submit = async (event, { close = false } = {}) => {
    event.preventDefault();
    setError("");
    if (!validate()) return;
    closeAfterRef.current = close;
    setSaving(true);
    try {
      if (!isNew) {
        await saveEdit();
        return;
      }
      const future = await api.futureAppointments(companyId);
      if (future.length > 0) {
        pendingRef.current = { ...headerPayload(), items: itemsPayload(items) };
        setFutures(future);
        setMergeChoice(String(future[0].id));
        setMergeOpen(true);
        return;
      }
      await createNew();
    } catch (err) {
      setError(err.message);
    } finally {
      setSaving(false);
    }
  };

  const confirmCreate = async () => {
    setError("");
    setSaving(true);
    try {
      await createNew();
      setMergeOpen(false);
    } catch (err) {
      setError(err.message);
    } finally {
      setSaving(false);
    }
  };

  const confirmAttach = async () => {
    if (!mergeChoice) return;
    setError("");
    setSaving(true);
    try {
      const payload = pendingRef.current;
      const nextItems = payload?.items || [];
      if (nextItems.length === 0) {
        setMergeOpen(false);
        navigate(closeAfterRef.current ? "/appointments" : `/appointments/${mergeChoice}`);
        return;
      }
      const attached = await api.addAppointmentItems(mergeChoice, nextItems);
      setMergeOpen(false);
      navigate(closeAfterRef.current ? "/appointments" : `/appointments/${attached.id}`);
    } catch (err) {
      setError(err.message);
    } finally {
      setSaving(false);
    }
  };

  const addPicked = async () => {
    setError("");
    try {
      let next = null;
      if (pickKind === "issue") {
        const issue = companyIssues.find((x) => String(x.id) === String(pickIssueId));
        if (!issue) {
          setError(companyId ? "請選擇該客戶的正式議題" : "請先選擇客戶公司");
          return;
        }
        next = {
          kind: "issue",
          issueId: issue.id,
          title: issue.issueNo ? `#${issue.issueNo} ${issue.title}` : issue.title,
          workLabel: issue.title
        };
      } else if (pickKind === "project") {
        const project = companyProjects.find((x) => String(x.id) === String(pickProjectId));
        if (!project) {
          setError(companyId ? "請選擇該客戶的專案" : "請先選擇客戶公司");
          return;
        }
        next = {
          kind: "project",
          projectId: project.id,
          title: `${project.code} ${project.name}`.trim(),
          workLabel: project.name
        };
      } else {
        const todo = todoOptions.find((x) => String(x.id) === String(pickTodoId));
        if (!todo) {
          setError("請選擇待辦");
          return;
        }
        const work = pickWorkKind === "issue"
          ? companyIssues.find((x) => String(x.id) === String(pickIssueId))
          : companyProjects.find((x) => String(x.id) === String(pickProjectId));
        next = {
          kind: "todo",
          todoId: Number(pickTodoId),
          issueId: pickWorkKind === "issue" ? Number(pickIssueId) : undefined,
          projectId: pickWorkKind === "project" ? Number(pickProjectId) : undefined,
          title: todo.title,
          workLabel: work ? (work.issueNo ? `#${work.issueNo} ${work.title}` : `${work.code || ""} ${work.name || work.title || ""}`.trim()) : ""
        };
      }
      if (items.some((item) => itemKey(item) === itemKey(next))) {
        setError("此處理事項已在本預約中");
        return;
      }
      if (isNew) {
        setItems((prev) => [...prev, next]);
        return;
      }
      const row = await api.addAppointmentItems(id, itemsPayload([next]));
      setItems(row.items);
    } catch (err) {
      setError(err.message);
    }
  };

  const removeItem = async (item) => {
    setError("");
    try {
      if (isNew || !item.id) {
        setItems((prev) => prev.filter((x) => itemKey(x) !== itemKey(item)));
        return;
      }
      const row = await api.deleteAppointmentItem(id, item.id);
      setItems(row.items);
    } catch (err) {
      setError(err.message);
    }
  };

  return (
    <div className="appointment-page">
      <div className="row-between">
        <PageTitle icon={CalendarClock}>{isNew ? "新增預約連線" : "編輯預約連線"}</PageTitle>
        <Link className="btn secondary" to="/appointments">
          <ArrowLeft size={16} strokeWidth={1.75} aria-hidden="true" />
          返回清單
        </Link>
      </div>
      {error && <p className="banner error">{error}</p>}
      {!statusMeta.majorCategoryId && (
        <p className="banner error">找不到大分類「預約」，請先到分類設定新增或改回此名稱。</p>
      )}
      <form className="card form" onSubmit={submit}>
        <label>
          客戶公司
          <VendorSelect
            value={companyId}
            disabled={companyLocked}
            onChange={changeCompany}
          />
        </label>
        <div className="two-col">
          <label>
            客戶窗口
            <AppSelect
              value={contactId}
              placeholder={companyId ? "請選擇窗口" : "請先選擇客戶公司"}
              options={[
                { value: "", label: companyId ? "請選擇窗口" : "請先選擇客戶公司" },
                ...contacts.map((c) => ({ value: c.id, label: c.name }))
              ]}
              onChange={changeContact}
            />
          </label>
          <label>
            聯繫方式
            <AppSelect
              value={channelId}
              placeholder={!contactId ? "請先選擇窗口" : (selectedChannels.length ? "請選擇聯繫方式" : "尚無聯絡資料")}
              options={[
                { value: "", label: !contactId ? "請先選擇窗口" : (selectedChannels.length ? "空白" : "尚無聯絡資料") },
                ...selectedChannels.map((ch) => ({
                  value: ch.id,
                  label: channelOptionLabel(ch)
                }))
              ]}
              onChange={setChannelId}
            />
          </label>
        </div>
        <div className="two-col">
          <label>
            預約狀態
            <AppSelect
              colored
              value={statusId}
              placeholder="空白"
              options={[
                { value: "", label: "空白" },
                ...statusOptions.map((s) => ({ value: s.id, label: s.name, color: statusMeta.color || s.color }))
              ]}
              onChange={setStatusId}
            />
          </label>
          <label>
            預約日期
            <AppDateField value={date} onChange={setDate} />
          </label>
        </div>
        <div className="btn-row">
          <button className="btn" disabled={saving} type="submit">
            {saving ? "儲存中…" : "儲存"}
          </button>
          <button className="btn" disabled={saving} type="button" onClick={(event) => submit(event, { close: true })}>
            {saving ? "儲存中…" : "儲存並關閉"}
          </button>
          {!isNew && (
            <button className="btn danger" type="button" onClick={() => setAskDelete(true)}>
              刪除
            </button>
          )}
        </div>
      </form>

      <section className="card">
        <PageTitle as="h2" icon={CalendarClock}>連線處理事項</PageTitle>
        {items.length === 0 && <p className="muted">尚無處理事項。</p>}
        <ul className="issue-list">
          {items.map((item) => (
            <li key={itemKey(item) || item.id} className="issue-row">
              <span className="appointment-item-copy">
                <span className="appointment-item-title-row">
                  <strong className="appointment-item-title">{item.title}</strong>
                  <button
                    className="btn secondary appointment-item-detail"
                    type="button"
                    onClick={() => setPreviewItem(item)}
                  >
                    詳情
                  </button>
                </span>
                {item.kind === "todo" && item.workLabel ? (
                  <em className="muted appointment-item-owner">{item.workLabel}</em>
                ) : null}
              </span>
              <button className="todo-icon-btn danger" type="button" aria-label="移除" onClick={() => removeItem(item)}>
                <Trash2 size={16} strokeWidth={1.75} />
              </button>
            </li>
          ))}
        </ul>
        <div className="form appointment-picker">
          {!companyId && <p className="muted">請先選擇客戶公司，才可挑選該公司的議題、專案或待辦。</p>}
          <label>
            種類
            <AppSelect
              value={pickKind}
              options={[
                { value: "issue", label: "正式議題" },
                { value: "project", label: "專案" },
                { value: "todo", label: "一般 TODO" }
              ]}
              onChange={(kind) => {
                setPickKind(kind);
                setPickTodoId("");
              }}
            />
          </label>
          {pickKind === "todo" && (
            <label>
              所屬工作
              <AppSelect
                value={pickWorkKind}
                options={[
                  { value: "issue", label: "正式議題" },
                  { value: "project", label: "專案" }
                ]}
                onChange={(kind) => {
                  setPickWorkKind(kind);
                  setPickTodoId("");
                  setTodoOptions([]);
                }}
              />
            </label>
          )}
          {(pickKind === "issue" || (pickKind === "todo" && pickWorkKind === "issue")) && (
            <label>
              正式議題
              <AppSelect
                value={pickIssueId}
                placeholder={!companyId ? "請先選擇客戶公司" : (companyIssues.length ? "請選擇議題" : "該客戶尚無正式議題")}
                options={[
                  { value: "", label: !companyId ? "請先選擇客戶公司" : (companyIssues.length ? "請選擇議題" : "該客戶尚無正式議題") },
                  ...companyIssues.map((x) => ({
                    value: x.id,
                    label: x.issueNo ? `#${x.issueNo} ${x.title}` : x.title
                  }))
                ]}
                onChange={async (value) => {
                  setPickIssueId(value);
                  setPickTodoId("");
                  if (pickKind === "todo") await loadTodoOptions("issue", value);
                }}
              />
            </label>
          )}
          {(pickKind === "project" || (pickKind === "todo" && pickWorkKind === "project")) && (
            <label>
              專案
              <AppSelect
                value={pickProjectId}
                placeholder={!companyId ? "請先選擇客戶公司" : (companyProjects.length ? "請選擇專案" : "該客戶尚無專案")}
                options={[
                  { value: "", label: !companyId ? "請先選擇客戶公司" : (companyProjects.length ? "請選擇專案" : "該客戶尚無專案") },
                  ...companyProjects.map((x) => ({
                    value: x.id,
                    label: `${x.code} ${x.name}`.trim()
                  }))
                ]}
                onChange={async (value) => {
                  setPickProjectId(value);
                  setPickTodoId("");
                  if (pickKind === "todo") await loadTodoOptions("project", value);
                }}
              />
            </label>
          )}
          {pickKind === "todo" && (
            <label>
              待辦
              <AppSelect
                value={pickTodoId}
                placeholder="請選擇待辦"
                options={[
                  { value: "", label: todoOptions.length ? "請選擇待辦" : "請先選工作" },
                  ...todoOptions.map((x) => ({ value: x.id, label: x.title }))
                ]}
                onChange={setPickTodoId}
              />
            </label>
          )}
          <button className="btn" type="button" onClick={addPicked}>
            加入處理事項
          </button>
        </div>
      </section>

      <dialog
        ref={mergeDialogRef}
        className="app-dialog"
        onCancel={() => setMergeOpen(false)}
        onClick={(event) => {
          if (event.target === mergeDialogRef.current) setMergeOpen(false);
        }}
      >
        <div className="dialog-panel is-wide form">
          <h3>已有未來預約</h3>
          <p>該客戶已有尚未結束且日期在未來的預約。請選擇再新增一筆，或把本次處理事項加入既有預約。</p>
          <div className="appointment-future-list">
            {futures.map((row) => (
              <label key={row.id} className="appointment-future-item">
                <input
                  type="radio"
                  name="future-appointment"
                  value={row.id}
                  checked={String(mergeChoice) === String(row.id)}
                  onChange={() => setMergeChoice(String(row.id))}
                />
                <span>
                  <strong>{row.clientCompanyName}</strong> {row.clientContactName}
                  {row.contactChannelLabel ? ` · ${row.contactChannelLabel}` : ""}
                  <br />
                  <span className="muted">{row.appointmentDate || "無日期"} · {row.statusName || "空白"}</span>
                  <br />
                  <span className="muted">{row.itemSummary}</span>
                </span>
              </label>
            ))}
          </div>
          <div className="btn-row dialog-actions">
            <button type="button" onClick={() => setMergeOpen(false)}>
              取消
            </button>
            <button className="btn" type="button" disabled={saving} onClick={confirmCreate}>
              再新增一筆
            </button>
            <button className="btn" type="button" disabled={saving || !mergeChoice} onClick={confirmAttach}>
              加入既有
            </button>
          </div>
        </div>
      </dialog>

      <AppointmentWorkPreview item={previewItem} onClose={() => setPreviewItem(null)} />
      <AppDialog
        open={askDelete}
        title="刪除預約連線"
        message="確定刪除這筆預約連線及其處理事項？"
        confirmLabel="刪除"
        danger
        onCancel={() => setAskDelete(false)}
        onSubmit={async () => {
          try {
            await api.deleteAppointment(id);
            navigate("/appointments");
          } catch (err) {
            setError(err.message);
            setAskDelete(false);
          }
        }}
      />
    </div>
  );
}
