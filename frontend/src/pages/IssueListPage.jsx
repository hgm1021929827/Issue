import { useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { ClipboardList, FileSpreadsheet, Plus, Sparkles } from "lucide-react";
import { api } from "../api.js";
import PageTitle from "../components/PageTitle.jsx";
import ImportIssueDialog from "../components/ImportIssueDialog.jsx";

function dueClass(dueDate) {
  if (!dueDate) return "due muted";
  const today = new Date();
  today.setHours(0, 0, 0, 0);
  const due = new Date(dueDate);
  due.setHours(0, 0, 0, 0);
  const diff = (due - today) / 86400000;
  if (diff < 0) return "due is-overdue";
  if (diff <= 3) return "due is-soon";
  return "due";
}

export default function IssueListPage() {
  const [majors, setMajors] = useState([]);
  const [filter, setFilter] = useState("");
  const [issues, setIssues] = useState([]);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  const [askImport, setAskImport] = useState(false);
  const navigate = useNavigate();

  const load = async () => {
    setError("");
    setLoading(true);
    try {
      const [majorList, list] = await Promise.all([
        api.majorCategories(),
        api.issues(filter || undefined)
      ]);
      setMajors(majorList);
      setIssues(list);
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
  }, [filter]);

  return (
    <div>
      {error && <p className="banner error">{error}</p>}
      <div className="row-between">
        <PageTitle icon={ClipboardList}>議題</PageTitle>
        <div className="btn-row">
          <button className="btn" type="button" onClick={() => { setError(""); setAskImport(true); }}>
            <FileSpreadsheet size={16} strokeWidth={1.75} aria-hidden="true" />
            匯入議題
          </button>
          <Link className="btn" to="/issues/new">
            <Plus size={16} strokeWidth={1.75} aria-hidden="true" />
            新增議題
          </Link>
        </div>
      </div>
      <div className="filters">
        <button className={!filter ? "chip active" : "chip"} type="button" onClick={() => setFilter("")}>
          全部
        </button>
        {majors.map((m) => (
          <button
            key={m.id}
            type="button"
            className={String(filter) === String(m.id) ? "chip active" : "chip"}
            onClick={() => setFilter(m.id)}
          >
            {m.name}
          </button>
        ))}
      </div>
      {loading && <p className="muted">載入中…</p>}
      {!loading && issues.length === 0 && (
        <div className="empty">
          <Sparkles className="empty-icon" size={22} strokeWidth={1.75} aria-hidden="true" />
          <p>尚無議題。</p>
          <Link className="btn" to="/issues/new">
            立即新增
          </Link>
        </div>
      )}
      <ul className="issue-list">
        {issues.map((item) => (
          <li key={item.id} className="issue-row">
            <Link to={`/issues/${item.id}`} style={{ "--accent-color": item.subCategoryColor || item.majorCategoryColor }}>
              <span className="tag">{item.subCategoryName || item.majorCategoryName}</span>
              <strong>
                <span className="issue-id">#{item.issueNo || item.id}</span>
                {item.title}
                {item.clientCompanyName ? <span className="list-vendor">{item.clientCompanyName}</span> : null}
                {item.missingKept ? <span className="tag is-kept">檔中沒有</span> : null}
              </strong>
              <em className={dueClass(item.dueDate)}>{item.dueDate || "無預計日"}</em>
            </Link>
          </li>
        ))}
      </ul>
      <ImportIssueDialog
        open={askImport}
        onClose={() => setAskImport(false)}
        onError={setError}
        onImported={(result) => navigate("/issues/import-result", { state: result })}
      />
    </div>
  );
}
