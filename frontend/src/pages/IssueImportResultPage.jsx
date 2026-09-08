import { useEffect, useMemo, useRef, useState } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import { FileSpreadsheet } from "lucide-react";
import { api } from "../api.js";
import PageTitle from "../components/PageTitle.jsx";

function formatDateTime(value) {
  if (!value) return "—";
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return String(value);
  const pad = (n) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())} ${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

function dash(value) {
  return value == null || String(value).trim() === "" ? "—" : value;
}

export default function IssueImportResultPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const result = location.state;
  const [decisions, setDecisions] = useState({});
  const [categoryChoices, setCategoryChoices] = useState({});
  const [error, setError] = useState("");
  const [saving, setSaving] = useState(false);
  const [previewId, setPreviewId] = useState(null);
  const [preview, setPreview] = useState(null);
  const [previewError, setPreviewError] = useState("");
  const [previewBusy, setPreviewBusy] = useState(false);
  const previewCache = useRef({});

  const notFound = result?.notFound || [];
  const pendingCategory = result?.pendingCategory || [];
  const ready = useMemo(
    () =>
      notFound.every((row) => decisions[String(row.id)])
      && pendingCategory.every((row) => categoryChoices[String(row.id)]),
    [notFound, decisions, pendingCategory, categoryChoices]
  );

  const closePreview = () => {
    setPreviewId(null);
    setPreview(null);
    setPreviewError("");
    setPreviewBusy(false);
  };

  useEffect(() => {
    if (previewId == null) return undefined;
    const onKey = (event) => {
      if (event.key === "Escape") closePreview();
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [previewId]);

  const openPreview = async (id) => {
    setPreviewId(id);
    setPreviewError("");
    const cached = previewCache.current[id];
    if (cached) {
      setPreview(cached);
      return;
    }
    setPreview(null);
    setPreviewBusy(true);
    try {
      const issue = await api.issue(id);
      previewCache.current[id] = issue;
      setPreview(issue);
    } catch (err) {
      setPreviewError(err.message);
    } finally {
      setPreviewBusy(false);
    }
  };

  if (!result) {
    return (
      <div className="card">
        <PageTitle icon={FileSpreadsheet}>匯入結果</PageTitle>
        <p className="banner error">找不到本次匯入結果，請重新匯入。</p>
        <button className="btn" type="button" onClick={() => navigate("/issues")}>
          回議題
        </button>
      </div>
    );
  }

  const summary = result.summary || {};
  const createdCompanies = result.createdCompanies || [];
  const rowErrors = result.rowErrors || [];

  return (
    <div>
      {error && <p className="banner error">{error}</p>}
      <div className="card">
        <PageTitle icon={FileSpreadsheet}>匯入結果</PageTitle>
        <div className="import-summary-grid">
          <section className="import-summary-group">
            <h3>正式議題</h3>
            <ul>
              <li><span>新增</span><strong>{summary.created || 0}</strong></li>
              <li><span>更新</span><strong>{summary.updated || 0}</strong></li>
              <li><span>略過</span><strong>{summary.skipped || 0}</strong></li>
            </ul>
          </section>
        </div>
      </div>

      {createdCompanies.length > 0 && (
        <section className="card">
          <h2>新建客戶公司</h2>
          <ul className="import-diff-list">
            {createdCompanies.map((row) => (
              <li key={row.id}>{row.name}</li>
            ))}
          </ul>
        </section>
      )}

      {rowErrors.length > 0 && (
        <section className="card">
          <h2>列異常</h2>
          <ul className="import-diff-list">
            {rowErrors.map((row, index) => (
              <li key={`${row.rowIndex}-${row.kind}-${index}`}>
                第 {row.rowIndex} 列：{row.message}
              </li>
            ))}
          </ul>
        </section>
      )}

      {pendingCategory.length > 0 && (
        <section className="card">
          <h2>請判斷加簽或結案</h2>
          <p className="muted">這些議題檔內狀態為處理中、已在系統中，目前簽核者不是你、但工程師是你。請逐筆選擇加簽或結案。</p>
          <ul className="import-decision-list">
            {pendingCategory.map((row) => {
              const key = String(row.id);
              return (
                <li key={`cat-${key}`}>
                  <strong className="import-decision-title">
                    #{row.issueNo} {row.title}
                    <span className="muted"> 檔內狀態：{row.status ? row.status : "（空白）"}</span>
                  </strong>
                  <button className="btn import-decision-open" type="button" onClick={() => openPreview(row.id)}>
                    查看
                  </button>
                  <span className="import-decision-actions">
                    <label>
                      <input
                        type="radio"
                        name={`cat-${key}`}
                        value="countersign"
                        checked={categoryChoices[key] === "countersign"}
                        onChange={() => setCategoryChoices((prev) => ({ ...prev, [key]: "countersign" }))}
                      />
                      加簽
                    </label>
                    <label>
                      <input
                        type="radio"
                        name={`cat-${key}`}
                        value="done"
                        checked={categoryChoices[key] === "done"}
                        onChange={() => setCategoryChoices((prev) => ({ ...prev, [key]: "done" }))}
                      />
                      結案
                    </label>
                  </span>
                </li>
              );
            })}
          </ul>
        </section>
      )}

      {notFound.length > 0 && (
        <section className="card">
          <h2>檔中沒有的資料</h2>
          <p className="muted">請為每一筆選擇保留或刪除後再確認。未確認就離開時，本次新增與更新仍會保留。</p>
          <ul className="import-decision-list">
            {notFound.map((row) => {
              const key = String(row.id);
              return (
                <li key={key}>
                  <strong className="import-decision-title">
                    #{row.issueNo} {row.title}
                  </strong>
                  <button className="btn import-decision-open" type="button" onClick={() => openPreview(row.id)}>
                    查看
                  </button>
                  <span className="import-decision-actions">
                    <label>
                      <input
                        type="radio"
                        name={key}
                        value="keep"
                        checked={decisions[key] === "keep"}
                        onChange={() => setDecisions((prev) => ({ ...prev, [key]: "keep" }))}
                      />
                      保留
                    </label>
                    <label>
                      <input
                        type="radio"
                        name={key}
                        value="delete"
                        checked={decisions[key] === "delete"}
                        onChange={() => setDecisions((prev) => ({ ...prev, [key]: "delete" }))}
                      />
                      刪除
                    </label>
                  </span>
                </li>
              );
            })}
          </ul>
        </section>
      )}

      <div className="btn-row">
        <button
          className="btn"
          type="button"
          disabled={saving || ((notFound.length > 0 || pendingCategory.length > 0) && !ready)}
          onClick={async () => {
            if (notFound.length > 0 && notFound.some((row) => !decisions[String(row.id)])) {
              setError("請為每筆未找到資料選擇保留或刪除");
              return;
            }
            if (pendingCategory.length > 0 && pendingCategory.some((row) => !categoryChoices[String(row.id)])) {
              setError("請為每筆選擇加簽或結案");
              return;
            }
            setSaving(true);
            setError("");
            try {
              if (notFound.length > 0 || pendingCategory.length > 0) {
                await api.applyIssueImportDecisions(
                  notFound.map((row) => ({
                    id: row.id,
                    action: decisions[String(row.id)]
                  })),
                  pendingCategory.map((row) => ({
                    id: row.id,
                    action: categoryChoices[String(row.id)]
                  })),
                  {
                    countersignSubCategoryId: result.countersignSubCategoryId,
                    doneSubCategoryId: result.doneSubCategoryId
                  }
                );
              }
              navigate("/issues");
            } catch (err) {
              setError(err.message);
            } finally {
              setSaving(false);
            }
          }}
        >
          {saving ? "處理中…" : "確認"}
        </button>
        <button className="btn" type="button" onClick={() => navigate("/issues")}>
          稍後再決定
        </button>
      </div>

      {previewId != null && (
        <dialog
          className="app-dialog issue-preview-dialog"
          open
          onClick={(event) => {
            if (event.target === event.currentTarget) closePreview();
          }}
        >
          <div className="dialog-panel is-wide form">
            <h3>{preview ? `議題 #${preview.issueNo}` : "查看議題"}</h3>
            {previewBusy && <p className="muted">載入中…</p>}
            {previewError && <p className="banner error">{previewError}</p>}
            {preview && (
              <dl className="readonly-fields">
                <div><dt>編號</dt><dd>{dash(preview.issueNo)}</dd></div>
                <div><dt>標題</dt><dd>{dash(preview.title)}</dd></div>
                <div className="readonly-span">
                  <dt>內容</dt>
                  <dd className="import-preview-text">{dash(preview.content)}</dd>
                </div>
                <div><dt>分類</dt><dd>{dash(preview.subCategoryName)}</dd></div>
                <div><dt>客戶公司</dt><dd>{dash(preview.clientCompanyName)}</dd></div>
                <div><dt>預計完成日</dt><dd>{dash(preview.dueDate)}</dd></div>
                <div className="readonly-span">
                  <dt>備註</dt>
                  <dd className="import-preview-text">{dash(preview.remark)}</dd>
                </div>
                <div><dt>建立時間</dt><dd>{formatDateTime(preview.createdAt)}</dd></div>
                <div><dt>更新時間</dt><dd>{formatDateTime(preview.updatedAt)}</dd></div>
              </dl>
            )}
            <div className="btn-row dialog-actions">
              <button type="button" className="btn" onClick={closePreview}>
                關閉
              </button>
            </div>
          </div>
        </dialog>
      )}
    </div>
  );
}
