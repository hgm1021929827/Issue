import { useEffect, useState } from "react";
import { Link, useNavigate, useSearchParams } from "react-router-dom";
import { ClipboardList, FileSpreadsheet, Plus, Search, Sparkles, X } from "lucide-react";
import { api } from "../api.js";
import { rememberIssueListSearch } from "../issueListSearch.js";
import AppSelect from "../components/AppSelect.jsx";
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
  const [searchParams, setSearchParams] = useSearchParams();
  const filter = searchParams.get("subCategoryId") || "";
  const vendorId = searchParams.get("clientCompanyId") || "";
  const appliedKeyword = searchParams.get("q") || "";
  const [subs, setSubs] = useState([]);
  const [companies, setCompanies] = useState([]);
  const [keyword, setKeyword] = useState(appliedKeyword);
  const [issues, setIssues] = useState([]);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  const [askImport, setAskImport] = useState(false);
  const navigate = useNavigate();

  const applySearch = () => setListParams({ q: keyword.trim() });
  const hasFilter = Boolean(filter || vendorId || appliedKeyword);

  const setListParams = (patch) => {
    const next = {
      subCategoryId: patch.subCategoryId !== undefined ? patch.subCategoryId : filter,
      clientCompanyId: patch.clientCompanyId !== undefined ? patch.clientCompanyId : vendorId,
      q: patch.q !== undefined ? patch.q : appliedKeyword
    };
    const params = new URLSearchParams();
    if (next.subCategoryId) params.set("subCategoryId", String(next.subCategoryId));
    if (next.clientCompanyId) params.set("clientCompanyId", String(next.clientCompanyId));
    if (next.q) params.set("q", next.q);
    setSearchParams(params, { replace: true });
  };

  const loadMeta = async () => {
    const [majorList, companyList] = await Promise.all([
      api.majorCategories(),
      api.clientCompanies()
    ]);
    const issueMajor = majorList.find((m) => m.name === "議題")
      || majorList.find((m) => m.name === "議題分類");
    setSubs(issueMajor ? await api.subCategories(issueMajor.id) : []);
    setCompanies(companyList);
  };

  const loadIssues = async () => {
    setError("");
    setLoading(true);
    try {
      setIssues(await api.issues({
        subCategoryId: filter || undefined,
        clientCompanyId: vendorId || undefined,
        q: appliedKeyword || undefined
      }));
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    setKeyword(appliedKeyword);
  }, [appliedKeyword]);

  useEffect(() => {
    rememberIssueListSearch(searchParams.toString());
  }, [searchParams]);

  useEffect(() => {
    loadMeta().catch((err) => setError(err.message));
  }, []);

  useEffect(() => {
    loadIssues();
  }, [filter, vendorId, appliedKeyword]);

  const clearMissingKept = async (event, issueId) => {
    event.preventDefault();
    event.stopPropagation();
    setError("");
    try {
      await api.clearIssueMissingKept(issueId);
      setIssues((rows) => rows.map((row) => (
        row.id === issueId ? { ...row, missingKept: false } : row
      )));
    } catch (err) {
      setError(err.message);
    }
  };

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
        <button className={!filter ? "chip active" : "chip"} type="button" onClick={() => setListParams({ subCategoryId: "" })}>
          全部
        </button>
        {subs.map((s) => (
          <button
            key={s.id}
            type="button"
            className={String(filter) === String(s.id) ? "chip active" : "chip"}
            onClick={() => setListParams({ subCategoryId: s.id })}
          >
            {s.name}
          </button>
        ))}
      </div>
      <div className="issue-list-tools">
        <AppSelect
          value={vendorId}
          placeholder="全部廠商"
          searchable
          searchPlaceholder="搜尋廠商"
          options={[
            { value: "", label: "全部廠商" },
            ...companies.map((c) => ({ value: c.id, label: c.name }))
          ]}
          onChange={(clientCompanyId) => setListParams({ clientCompanyId })}
        />
        <input
          type="search"
          placeholder="搜尋編號或標題"
          value={keyword}
          aria-label="搜尋編號或標題"
          onChange={(e) => setKeyword(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === "Enter") applySearch();
          }}
        />
        <button type="button" className="btn" onClick={applySearch}>
          <Search size={16} strokeWidth={1.75} aria-hidden="true" />
          搜尋
        </button>
      </div>
      {loading && <p className="muted">載入中…</p>}
      {!loading && issues.length === 0 && (
        <div className="empty">
          <Sparkles className="empty-icon" size={22} strokeWidth={1.75} aria-hidden="true" />
          {hasFilter ? (
            <p>沒有符合的議題。</p>
          ) : (
            <>
              <p>尚無議題。</p>
              <Link className="btn" to="/issues/new">
                立即新增
              </Link>
            </>
          )}
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
                {item.missingKept ? (
                  <span className="tag is-kept">
                    檔中沒有
                    <button
                      type="button"
                      className="kept-clear"
                      title="取消檔中沒有"
                      aria-label="取消檔中沒有"
                      onClick={(event) => clearMissingKept(event, item.id)}
                    >
                      <X size={12} strokeWidth={2.25} aria-hidden="true" />
                    </button>
                  </span>
                ) : null}
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
