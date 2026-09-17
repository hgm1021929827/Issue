import { useEffect, useState } from "react";
import { useNavigate, useParams, useSearchParams } from "react-router-dom";
import { ArrowLeft, FilePlus2, NotebookPen, X } from "lucide-react";
import { api } from "../api.js";
import { getIssueListPath } from "../issueListSearch.js";
import TodoTree from "../components/TodoTree.jsx";
import TrackTodoList from "../components/TrackTodoList.jsx";
import WorkHourTable from "../components/WorkHourTable.jsx";
import AppDialog from "../components/AppDialog.jsx";
import AppSelect from "../components/AppSelect.jsx";
import AppDateField from "../components/AppDateField.jsx";
import AppTextarea from "../components/AppTextarea.jsx";
import PageTitle from "../components/PageTitle.jsx";
import VendorSelect from "../components/VendorSelect.jsx";

const emptyForm = {
  issueNo: "",
  title: "",
  content: "",
  majorCategoryId: 1,
  subCategoryId: "",
  dueDate: "",
  remark: "",
  clientCompanyId: "",
  importedFromUof: false,
  missingKept: false,
  createdAt: "",
  updatedAt: ""
};

function formatDateTime(value) {
  if (!value) return "—";
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return String(value);
  const pad = (n) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

export default function IssuePage() {
  const { id } = useParams();
  const isNew = !id;
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const highlightTrackId = searchParams.get("trackTodo");
  const [subs, setSubs] = useState([]);
  const [form, setForm] = useState(emptyForm);
  const [todos, setTodos] = useState([]);
  const [tracks, setTracks] = useState([]);
  const [tracksReady, setTracksReady] = useState(false);
  const [hours, setHours] = useState([]);
  const [error, setError] = useState("");
  const [saving, setSaving] = useState(false);
  const [askDelete, setAskDelete] = useState(false);

  const hoursTotal = hours.reduce((sum, row) => sum + Number(row.hours || 0), 0);
  const goToIssueList = () => navigate(getIssueListPath());

  const loadSubs = async (majorId, keepSub) => {
    const list = await api.subCategories(majorId);
    setSubs(list);
    if (keepSub && list.some((s) => String(s.id) === String(keepSub))) {
      return keepSub;
    }
    return "";
  };

  useEffect(() => {
    setTracksReady(false);
    (async () => {
      try {
        const majorList = await api.majorCategories();
        if (isNew) {
          const issueMajor = majorList.find((m) => m.name === "議題")
            || majorList.find((m) => m.name === "議題分類")
            || majorList[0];
          await loadSubs(issueMajor?.id, "");
          setForm({ ...emptyForm, majorCategoryId: issueMajor?.id });
          setHours([]);
          return;
        }
        const [issue, tree, trackList, hourList] = await Promise.all([
          api.issue(id),
          api.todos(id),
          api.issueTrackTodos(id),
          api.issueHours(id)
        ]);
        await loadSubs(issue.majorCategoryId, issue.subCategoryId);
        setForm({
          issueNo: issue.issueNo || "",
          title: issue.title,
          content: issue.content || "",
          majorCategoryId: issue.majorCategoryId,
          subCategoryId: issue.subCategoryId || "",
          dueDate: issue.dueDate || "",
          remark: issue.remark || "",
          clientCompanyId: issue.clientCompanyId || "",
          importedFromUof: !!issue.importedFromUof,
          missingKept: !!issue.missingKept,
          createdAt: issue.createdAt || "",
          updatedAt: issue.updatedAt || ""
        });
        setTodos(tree);
        setTracks(trackList);
        setHours(hourList);
        setTracksReady(true);
      } catch (err) {
        setError(err.message);
      }
    })();
  }, [id]);

  const payload = () => ({
    issueNo: form.issueNo,
    title: form.title,
    content: form.content,
    majorCategoryId: Number(form.majorCategoryId),
    subCategoryId: form.subCategoryId === "" ? null : Number(form.subCategoryId),
    dueDate: form.dueDate || null,
    remark: form.remark || null,
    clientCompanyId: form.clientCompanyId === "" ? null : Number(form.clientCompanyId)
  });

  const saveIssue = async ({ close } = {}) => {
    if (form.clientCompanyId === "" || form.clientCompanyId == null) {
      setError("請選擇客戶公司");
      return false;
    }
    setSaving(true);
    setError("");
    try {
      if (isNew) {
        const created = await api.createIssue(payload());
        if (close) goToIssueList();
        else navigate(`/issues/${created.id}`);
      } else {
        const issue = await api.updateIssue(id, payload());
        setForm((prev) => ({
          ...prev,
          issueNo: issue.issueNo || prev.issueNo,
          title: issue.title,
          createdAt: issue.createdAt || prev.createdAt,
          updatedAt: issue.updatedAt || prev.updatedAt
        }));
        if (close) goToIssueList();
      }
      return true;
    } catch (err) {
      setError(err.message);
      return false;
    } finally {
      setSaving(false);
    }
  };

  const save = async (event) => {
    event.preventDefault();
    await saveIssue({ close: false });
  };

  const clearMissingKept = async () => {
    if (isNew) return;
    setError("");
    try {
      const issue = await api.clearIssueMissingKept(id);
      setForm((prev) => ({
        ...prev,
        missingKept: !!issue.missingKept,
        updatedAt: issue.updatedAt || prev.updatedAt
      }));
    } catch (err) {
      setError(err.message);
    }
  };

  const imported = !!form.importedFromUof;

  return (
    <div>
      {error && <p className="banner error">{error}</p>}
      <form className="card form" onSubmit={save}>
        <div className="row-between">
          <PageTitle icon={isNew ? FilePlus2 : NotebookPen}>
            {isNew ? "新增議題" : `議題詳情 #${form.issueNo || id}`}
            {form.missingKept ? (
              <span className="tag is-kept">
                檔中沒有
                <button
                  type="button"
                  className="kept-clear"
                  title="取消檔中沒有"
                  aria-label="取消檔中沒有"
                  onClick={clearMissingKept}
                >
                  <X size={12} strokeWidth={2.25} aria-hidden="true" />
                </button>
              </span>
            ) : null}
          </PageTitle>
          <button className="btn" type="button" onClick={goToIssueList}>
            <ArrowLeft size={16} strokeWidth={1.75} aria-hidden="true" />
            回到議題列表
          </button>
        </div>
        {imported && (
          <p className="muted">此議題由匯入產生，編號、標題、內容、預計完成日與廠商請重新匯入以更新。</p>
        )}
        <div className={`two-col${isNew ? "" : " issue-meta-fields"}`}>
          <label>
            編號
            <input
              required
              maxLength={50}
              readOnly={imported}
              value={form.issueNo}
              onChange={(e) => setForm({ ...form, issueNo: e.target.value })}
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
          標題
          <input
            value={form.title}
            onChange={(e) => setForm({ ...form, title: e.target.value })}
            required
            readOnly={imported}
          />
        </label>
        <label>
          內容
          <AppTextarea
            rows={4}
            value={form.content}
            onChange={(e) => setForm({ ...form, content: e.target.value })}
            readOnly={imported}
          />
        </label>
        <div className="two-col">
          <label>
            分類
            <AppSelect
              colored
              value={form.subCategoryId}
              placeholder="請選擇"
              options={[
                { value: "", label: "請選擇" },
                ...subs.map((s) => ({ value: s.id, label: s.name, color: s.color }))
              ]}
              onChange={(subCategoryId) => setForm({ ...form, subCategoryId })}
            />
          </label>
          <label>
            客戶公司
            <VendorSelect
              value={form.clientCompanyId}
              disabled={imported}
              onChange={(clientCompanyId) => setForm({ ...form, clientCompanyId })}
            />
          </label>
        </div>
        <label>
          預計完成日
          <AppDateField
            value={form.dueDate}
            disabled={imported}
            onChange={(dueDate) => setForm({ ...form, dueDate })}
          />
        </label>
        <label>
          備註
          <AppTextarea rows={2} value={form.remark} onChange={(e) => setForm({ ...form, remark: e.target.value })} />
        </label>
        <div className="btn-row">
          <button className="btn" disabled={saving} type="submit">
            {saving ? "儲存中…" : "儲存"}
          </button>
          <button className="btn" disabled={saving} type="button" onClick={() => saveIssue({ close: true })}>
            {saving ? "儲存中…" : "儲存並關閉"}
          </button>
          {!isNew && (
            <button className="btn danger" type="button" onClick={() => setAskDelete(true)}>
              刪除
            </button>
          )}
        </div>
      </form>
      {!isNew && (
        <WorkHourTable
          hours={hours}
          hoursTotal={hoursTotal}
          onError={setError}
          onCreate={async (payload) => {
            setHours(await api.createIssueHour(id, payload));
          }}
          onUpdate={async (hourId, payload) => {
            setHours(await api.updateIssueHour(id, hourId, payload));
          }}
          onDelete={async (hourId) => {
            setHours(await api.deleteIssueHour(id, hourId));
          }}
        />
      )}
      {!isNew && (
        <TrackTodoList
          items={tracks}
          onChange={setTracks}
          onError={setError}
          vendorCompanyId={form.clientCompanyId === "" ? null : form.clientCompanyId}
          create={(payload) => api.createIssueTrackTodo(id, payload)}
          highlightId={highlightTrackId}
          ready={tracksReady}
        />
      )}
      {!isNew && (
        <TodoTree
          issueId={id}
          nodes={todos}
          onChange={setTodos}
          onError={setError}
        />
      )}
      <AppDialog
        open={askDelete}
        title="刪除議題"
        message={tracks.length > 0
          ? `確定刪除這筆議題及其 TODO？將一併刪除 ${tracks.length} 筆需要追蹤的 TODO。`
          : "確定刪除這筆議題及其 TODO？"}
        confirmLabel="刪除"
        danger
        onCancel={() => setAskDelete(false)}
        onSubmit={async () => {
          try {
            await api.deleteIssue(id);
            goToIssueList();
          } catch (err) {
            setError(err.message);
            setAskDelete(false);
          }
        }}
      />
    </div>
  );
}
