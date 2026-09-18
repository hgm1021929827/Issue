import { useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { CalendarClock, Plus, Sparkles } from "lucide-react";
import { api } from "../api.js";
import AppDialog from "../components/AppDialog.jsx";
import PageTitle from "../components/PageTitle.jsx";
import AppointmentWorkPreview from "../components/AppointmentWorkPreview.jsx";

function itemKindLabel(item) {
  if (item.kind === "project") return "專案";
  if (item.kind === "todo") return "待辦";
  return "議題";
}

export default function AppointmentListPage() {
  const [rows, setRows] = useState([]);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(true);
  const [previewItem, setPreviewItem] = useState(null);
  const [askDelete, setAskDelete] = useState(null);
  const navigate = useNavigate();

  const load = async () => {
    setError("");
    setLoading(true);
    try {
      setRows(await api.appointments());
    } catch (err) {
      setError(err.message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    load();
  }, []);

  return (
    <div className="appointment-list-page">
      <div className="row-between">
        <PageTitle icon={CalendarClock}>預約連線</PageTitle>
        <button className="btn" type="button" onClick={() => navigate("/appointments/new")}>
          <Plus size={16} strokeWidth={1.75} aria-hidden="true" />
          新增預約連線
        </button>
      </div>
      {error && <p className="banner error">{error}</p>}
      {loading && <p className="muted">載入中…</p>}
      {!loading && rows.length === 0 && (
        <div className="empty card">
          <Sparkles className="empty-icon" size={22} strokeWidth={1.75} aria-hidden="true" />
          <p>尚無預約連線。</p>
        </div>
      )}
      {rows.length > 0 && (
        <ul className="appointment-list">
          {rows.map((row) => (
            <li key={row.id} className="appointment-row">
              <div className="appointment-head">
                <Link to={`/appointments/${row.id}`} className="appointment-head-main">
                  <span className="appointment-meta">
                    <span className="tag" style={{ "--accent-color": row.statusColor || undefined }}>
                      {row.statusName || "預約"}
                    </span>
                    <em className="due">{row.appointmentDate || "無日期"}</em>
                  </span>
                  <strong className="appointment-company">{row.clientCompanyName}</strong>
                  {row.clientContactName ? (
                    <span className="appointment-contact">
                      {row.clientContactName}
                      {row.contactChannelLabel ? ` · ${row.contactChannelLabel}` : ""}
                    </span>
                  ) : null}
                </Link>
                <div className="appointment-head-actions">
                  <Link className="btn secondary appointment-edit" to={`/appointments/${row.id}`}>
                    編輯
                  </Link>
                  <button className="btn danger" type="button" onClick={() => setAskDelete(row)}>
                    刪除
                  </button>
                </div>
              </div>
              {row.items.length === 0 ? (
                <p className="muted appointment-empty-items">尚無處理事項。</p>
              ) : (
                <ul className="appointment-items">
                  {row.items.map((item) => {
                    const title = item.title || item.workLabel || "（無標題）";
                    return (
                      <li key={item.id}>
                        <div className="appointment-item-line">
                          <span className="tag home-type-tag">{itemKindLabel(item)}</span>
                          <span className="appointment-item-copy">
                            <span className="appointment-item-title-row">
                              <span className="appointment-item-title">{title}</span>
                              <button
                                className="btn secondary appointment-item-detail"
                                type="button"
                                onClick={() => setPreviewItem(item)}
                              >
                                詳情
                              </button>
                            </span>
                            {item.kind === "todo" && item.workLabel ? (
                              <span className="muted appointment-item-owner">{item.workLabel}</span>
                            ) : null}
                          </span>
                        </div>
                      </li>
                    );
                  })}
                </ul>
              )}
            </li>
          ))}
        </ul>
      )}
      <AppointmentWorkPreview item={previewItem} onClose={() => setPreviewItem(null)} />
      <AppDialog
        open={Boolean(askDelete)}
        title="刪除預約連線"
        message={
          askDelete
            ? `確定刪除「${askDelete.clientCompanyName}」這筆預約連線及其處理事項？`
            : "確定刪除這筆預約連線及其處理事項？"
        }
        confirmLabel="刪除"
        danger
        onCancel={() => setAskDelete(null)}
        onSubmit={async () => {
          try {
            await api.deleteAppointment(askDelete.id);
            setAskDelete(null);
            await load();
          } catch (err) {
            setError(err.message);
            setAskDelete(null);
          }
        }}
      />
    </div>
  );
}
