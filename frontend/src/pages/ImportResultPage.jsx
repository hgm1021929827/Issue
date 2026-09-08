import { useEffect, useMemo, useRef, useState } from "react";
import { useLocation, useNavigate, useParams } from "react-router-dom";
import { FileSpreadsheet, Trash2 } from "lucide-react";
import { api } from "../api.js";
import PageTitle from "../components/PageTitle.jsx";

function ownerKindLabel(kind) {
  return kind === "workItem" ? "工作項次" : "專案議題";
}

function sameDate(a, b) {
  return String(a || "").slice(0, 10) === String(b || "").slice(0, 10);
}

function HourReviewCard({ projectId, review, registerApply, onError }) {
  const navigate = useNavigate();
  const key = `${review.ownerKind}:${review.ownerId}`;
  const [hours, setHours] = useState([]);
  const [lines, setLines] = useState(() =>
    (review.lines || []).map((line) => ({
      ...line,
      text: line.text || "",
      keep: false
    }))
  );

  const load = async () => {
    const list = review.ownerKind === "workItem"
      ? await api.workItemHours(projectId, review.ownerId)
      : await api.itemHours(projectId, review.ownerId);
    setHours(list);
    return list;
  };

  useEffect(() => {
    load().catch((err) => onError(err.message));
  }, [projectId, review.ownerKind, review.ownerId]);

  const updateHour = (hourId, payload) => review.ownerKind === "workItem"
    ? api.updateWorkItemHour(projectId, review.ownerId, hourId, payload)
    : api.updateItemHour(projectId, review.ownerId, hourId, payload);
  const deleteHour = (hourId) => review.ownerKind === "workItem"
    ? api.deleteWorkItemHour(projectId, review.ownerId, hourId)
    : api.deleteItemHour(projectId, review.ownerId, hourId);

  const applyKeep = async () => {
    const kept = lines.filter((line) => line.kind === "text" && line.keep && line.text.trim());
    if (kept.length === 0) return;
    const extra = kept.map((line) => line.text.trim()).join("\n");
    let ownerRemark = "";
    if (review.ownerKind === "workItem") {
      ownerRemark = (await api.projectWorkItem(projectId, review.ownerId)).remark || "";
    } else {
      const items = await api.projectItems(projectId);
      ownerRemark = items.find((row) => String(row.id) === String(review.ownerId))?.remark || "";
    }
    const current = ownerRemark.trim();
    const alreadyKept = extra.split("\n").every((line) => {
      const text = line.trim();
      return !text || current === text || current.endsWith(text) || current.includes(text)
        || current.split(/\r?\n/).some((row) => row.trim() === text);
    });
    const nextRemark = alreadyKept ? current : (current ? `${current}\n${extra}` : extra);
    if (review.ownerKind === "workItem") {
      await api.updateWorkItemRemark(projectId, review.ownerId, nextRemark);
    } else {
      await api.updateItemRemark(projectId, review.ownerId, nextRemark);
    }
    const list = hours.length ? hours : await load();
    for (const row of list) {
      const text = (row.remark || "").trim();
      if (!text) continue;
      if (text === extra || text.endsWith(`\n${extra}`)) {
        const stripped = text === extra ? "" : text.slice(0, text.length - extra.length).trim();
        await updateHour(row.id, {
          date: String(row.date).slice(0, 10),
          hours: Number(row.hours),
          remark: stripped
        });
      }
    }
    await load();
  };

  useEffect(() => {
    registerApply(key, applyKeep);
    return () => registerApply(key, null);
  }, [key, lines, hours]);

  const removeLine = async (line) => {
    onError("");
    if (line.kind === "hour" && line.date) {
      const existing = hours.find((row) => sameDate(row.date, line.date));
      if (existing) setHours(await deleteHour(existing.id));
    }
    setLines(lines.filter((row) => row.index !== line.index));
  };

  const openOwner = () => {
    const query = review.ownerKind === "workItem" ? `workItem=${review.ownerId}` : `item=${review.ownerId}`;
    navigate(`/projects/${projectId}?${query}`);
  };

  return (
    <article className="import-hour-card">
      <h3>
        {ownerKindLabel(review.ownerKind)} {review.ownerLabel}
      </h3>
      <p className="muted">依 Excel 備註原順序。無法解析的列可勾選保留到此項次／議題的備註最後一行。</p>
      <ul className="import-line-list">
        {lines.map((line) => (
          <li key={line.index} className={`import-line ${line.kind}`}>
            {line.kind === "hour" ? (
              <>
                <span className="import-line-date">{String(line.date || "").slice(0, 10)}</span>
                <span className="import-line-hours">{line.hours} 小時</span>
                <span />
              </>
            ) : (
              <>
                <input
                  value={line.text}
                  onChange={(e) => setLines(lines.map((row) => (
                    row.index === line.index ? { ...row, text: e.target.value } : row
                  )))}
                />
                <label className="import-keep">
                  <input
                    type="checkbox"
                    checked={line.keep}
                    onChange={(e) => setLines(lines.map((row) => (
                      row.index === line.index ? { ...row, keep: e.target.checked } : row
                    )))}
                  />
                  保留進項次備註最後一行
                </label>
              </>
            )}
            <button
              type="button"
              className="todo-icon-btn danger"
              title="刪除"
              aria-label="刪除此列"
              onClick={async () => {
                try {
                  await removeLine(line);
                } catch (err) {
                  onError(err.message);
                }
              }}
            >
              <Trash2 size={15} strokeWidth={1.75} aria-hidden="true" />
            </button>
          </li>
        ))}
      </ul>
      {lines.length === 0 && <p className="muted">此筆備註列已全部刪除。</p>}
      <div className="btn-row">
        <button className="btn" type="button" onClick={openOwner}>
          前往該筆
        </button>
      </div>
    </article>
  );
}

export default function ImportResultPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const location = useLocation();
  const result = location.state?.result;
  const applyFns = useRef(new Map());
  const [decisions, setDecisions] = useState(() => {
    const map = {};
    (result?.notFound || []).forEach((row) => {
      map[`${row.ownerKind}:${row.ownerId}`] = "";
    });
    return map;
  });
  const [error, setError] = useState("");
  const [saving, setSaving] = useState(false);

  const notFound = result?.notFound || [];
  const ready = useMemo(
    () => notFound.every((row) => decisions[`${row.ownerKind}:${row.ownerId}`]),
    [notFound, decisions]
  );
  const reviews = result?.hourReviews || [];

  const registerApply = (key, fn) => {
    if (fn) applyFns.current.set(key, fn);
    else applyFns.current.delete(key);
  };

  if (!result) {
    return (
      <div className="card">
        <PageTitle icon={FileSpreadsheet}>匯入結果</PageTitle>
        <p className="banner error">找不到本次匯入結果，請重新選擇檔案匯入。</p>
        <button className="btn" type="button" onClick={() => navigate(`/projects/${id}`)}>
          回專案
        </button>
      </div>
    );
  }

  const summary = result.summary || {};

  return (
    <div>
      {error && <p className="banner error">{error}</p>}
      <div className="card">
        <PageTitle icon={FileSpreadsheet}>匯入結果</PageTitle>
        <div className="import-summary-grid">
          <section className="import-summary-group">
            <h3>工作項次</h3>
            <ul>
              <li><span>新增</span><strong>{summary.workItemCreated || 0}</strong></li>
              <li><span>更新</span><strong>{summary.workItemUpdated || 0}</strong></li>
              <li><span>略過</span><strong>{summary.workItemSkipped || 0}</strong></li>
            </ul>
          </section>
          <section className="import-summary-group">
            <h3>專案議題</h3>
            <ul>
              <li><span>新增</span><strong>{summary.issueCreated || 0}</strong></li>
              <li><span>更新</span><strong>{summary.issueUpdated || 0}</strong></li>
              <li><span>略過</span><strong>{summary.issueSkipped || 0}</strong></li>
            </ul>
          </section>
        </div>
      </div>

      {(result.rowErrors || []).length > 0 && (
        <section className="card">
          <h2>列異常</h2>
          <ul className="import-diff-list">
            {result.rowErrors.map((row, index) => (
              <li key={`${row.sheet}-${row.rowIndex}-${index}`}>
                {row.sheet} 第 {row.rowIndex} 列：{row.message}
              </li>
            ))}
          </ul>
        </section>
      )}

      {reviews.length > 0 && (
        <section className="card">
          <h2>工時備註處理</h2>
          <p className="muted">可解析的工時已寫入。請逐行核對；勾選的文字會在按確認時接到該項次或議題的備註最後一行。</p>
          {reviews.map((review) => (
            <HourReviewCard
              key={`${review.ownerKind}:${review.ownerId}`}
              projectId={id}
              review={review}
              registerApply={registerApply}
              onError={setError}
            />
          ))}
        </section>
      )}

      {notFound.length > 0 && (
        <section className="card">
          <h2>檔中沒有的資料</h2>
          <p className="muted">請為每一筆選擇保留或刪除後再確認。</p>
          <ul className="import-decision-list">
            {notFound.map((row) => {
              const key = `${row.ownerKind}:${row.ownerId}`;
              return (
                <li key={key}>
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
                  <strong className="import-decision-title">
                    {ownerKindLabel(row.ownerKind)} {row.ownerLabel}
                  </strong>
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
          disabled={saving || (notFound.length > 0 && !ready)}
          onClick={async () => {
            if (notFound.length > 0 && !ready) {
              setError("請為每筆未找到資料選擇保留或刪除");
              return;
            }
            setSaving(true);
            setError("");
            try {
              for (const fn of applyFns.current.values()) {
                await fn();
              }
              if (notFound.length > 0) {
                await api.applyImportDecisions(
                  id,
                  notFound.map((row) => ({
                    ownerKind: row.ownerKind,
                    ownerId: row.ownerId,
                    action: decisions[`${row.ownerKind}:${row.ownerId}`]
                  }))
                );
              }
              navigate(`/projects/${id}?tab=workItems`);
            } catch (err) {
              setError(err.message);
            } finally {
              setSaving(false);
            }
          }}
        >
          {saving ? "儲存中…" : "確認"}
        </button>
        <button className="btn" type="button" onClick={() => navigate(`/projects/${id}`)}>
          稍後再決定
        </button>
      </div>
    </div>
  );
}
