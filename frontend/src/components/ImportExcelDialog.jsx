import { useEffect, useRef, useState } from "react";
import { FileSpreadsheet } from "lucide-react";
import { api } from "../api.js";
import AppDialog from "./AppDialog.jsx";
import AppSelect from "./AppSelect.jsx";
import { getRememberedImportMemberId, rememberImportMemberId, setPendingImport } from "../importSession.js";

export default function ImportExcelDialog({ open, onClose, onError, onReady, onNeedProject }) {
  const [members, setMembers] = useState([]);
  const [memberId, setMemberId] = useState("");
  const [file, setFile] = useState(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const fileRef = useRef(null);

  useEffect(() => {
    if (!open) return;
    setFile(null);
    setError("");
    if (fileRef.current) fileRef.current.value = "";
    api.companyMembers()
      .then((list) => {
        setMembers(list);
        const remembered = getRememberedImportMemberId();
        setMemberId(list.some((m) => String(m.id) === String(remembered)) ? remembered : "");
      })
      .catch((err) => setError(err.message));
  }, [open]);

  return (
    <AppDialog
      open={open}
      title="匯入專案 Excel"
      confirmLabel={busy ? "處理中…" : "開始匯入"}
      onCancel={onClose}
      onSubmit={async () => {
        if (members.length === 0) {
          setError("請先到成員頁新增公司成員");
          return;
        }
        if (!file) {
          setError("請選擇一個 Excel 檔");
          return;
        }
        if (!memberId) {
          setError("請先選擇目前使用者");
          return;
        }
        setBusy(true);
        setError("");
        try {
          rememberImportMemberId(memberId);
          const resolved = await api.resolveProjectImport(file);
          const session = {
            file,
            memberId: Number(memberId),
            projectCode: resolved.projectCode,
            suggestedName: resolved.suggestedName || ""
          };
          setPendingImport(session);
          if (resolved.status === "needProject") {
            onNeedProject?.(session);
          } else {
            onReady?.(resolved.projectId, session);
          }
          onClose();
        } catch (err) {
          setError(err.message);
        } finally {
          setBusy(false);
        }
      }}
    >
      {error && <p className="banner error">{error}</p>}
      {members.length === 0 ? (
        <p className="muted">尚無公司成員，請先到成員頁新增後再匯入。</p>
      ) : (
        <>
          <label>
            Excel 檔（xlsx）
            <input
              ref={fileRef}
              className="import-file-input"
              type="file"
              accept=".xlsx"
              tabIndex={-1}
              onChange={(e) => {
                setError("");
                setFile(e.target.files?.[0] || null);
              }}
            />
            <span className="import-file-row">
              <button type="button" className="btn" onClick={() => fileRef.current?.click()}>
                <FileSpreadsheet size={16} strokeWidth={1.75} aria-hidden="true" />
                選擇檔案
              </button>
              <span className={`import-file-name${file ? "" : " is-empty"}`}>
                {file ? file.name : "尚未選擇檔案"}
              </span>
            </span>
          </label>
          <label>
            目前使用者
            <AppSelect
              value={memberId}
              placeholder="請選擇"
              options={[
                { value: "", label: "請選擇" },
                ...members.map((m) => ({ value: m.id, label: `${m.name}（${m.englishName}）` }))
              ]}
              onChange={(value) => {
                setError("");
                setMemberId(value);
              }}
            />
          </label>
        </>
      )}
    </AppDialog>
  );
}
