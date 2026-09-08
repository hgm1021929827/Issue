import { useEffect, useRef, useState } from "react";
import { FileSpreadsheet } from "lucide-react";
import { api } from "../api.js";
import AppDialog from "./AppDialog.jsx";
import AppSelect from "./AppSelect.jsx";
import {
  getRememberedIssueImportCountersignSubId,
  getRememberedIssueImportDoneSubId,
  getRememberedIssueImportInProgressSubId,
  getRememberedIssueImportMemberId,
  rememberIssueImportCountersignSubId,
  rememberIssueImportDoneSubId,
  rememberIssueImportInProgressSubId,
  rememberIssueImportMemberId
} from "../importSession.js";

function pickIssueMajor(majors) {
  return majors.find((m) => m.name === "議題") || majors.find((m) => m.name === "議題分類") || null;
}

function pickSub(list, remembered, name) {
  if (remembered && list.some((s) => String(s.id) === String(remembered))) return remembered;
  return list.find((s) => s.name === name)?.id || "";
}

export default function ImportIssueDialog({ open, onClose, onImported, onError }) {
  const [members, setMembers] = useState([]);
  const [issueMajor, setIssueMajor] = useState(null);
  const [subs, setSubs] = useState([]);
  const [memberId, setMemberId] = useState("");
  const [inProgressId, setInProgressId] = useState("");
  const [countersignId, setCountersignId] = useState("");
  const [doneId, setDoneId] = useState("");
  const [file, setFile] = useState(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const fileRef = useRef(null);

  useEffect(() => {
    if (!open) return;
    setFile(null);
    setError("");
    if (fileRef.current) fileRef.current.value = "";
    Promise.all([api.companyMembers(), api.majorCategories()])
      .then(async ([memberList, majorList]) => {
        setMembers(memberList);
        const remembered = getRememberedIssueImportMemberId();
        setMemberId(memberList.some((m) => String(m.id) === String(remembered)) ? remembered : "");
        const major = pickIssueMajor(majorList);
        setIssueMajor(major);
        if (!major) {
          setSubs([]);
          setInProgressId("");
          setCountersignId("");
          setDoneId("");
          setError("找不到大分類「議題」");
          return;
        }
        const subList = await api.subCategories(major.id);
        setSubs(subList);
        setInProgressId(pickSub(subList, getRememberedIssueImportInProgressSubId(), "處理中"));
        setCountersignId(pickSub(subList, getRememberedIssueImportCountersignSubId(), "加簽"));
        setDoneId(pickSub(subList, getRememberedIssueImportDoneSubId(), "已結案"));
      })
      .catch((err) => setError(err.message));
  }, [open]);

  return (
    <AppDialog
      open={open}
      wide
      title="匯入議題"
      confirmLabel={busy ? "處理中…" : "開始匯入"}
      onCancel={onClose}
      onSubmit={async () => {
        if (members.length === 0) {
          setError("請先到成員頁新增公司成員");
          return;
        }
        if (!file) {
          setError("請選擇一個檔案");
          return;
        }
        if (!memberId) {
          setError("請先選擇目前使用者");
          return;
        }
        if (!inProgressId || !countersignId || !doneId) {
          setError("請選擇處理中、加簽與已結案小分類");
          return;
        }
        if (new Set([String(inProgressId), String(countersignId), String(doneId)]).size !== 3) {
          setError("處理中、加簽與已結案不可相同");
          return;
        }
        setBusy(true);
        setError("");
        try {
          rememberIssueImportMemberId(memberId);
          rememberIssueImportInProgressSubId(inProgressId);
          rememberIssueImportCountersignSubId(countersignId);
          rememberIssueImportDoneSubId(doneId);
          const result = await api.importIssues(file, memberId, inProgressId, countersignId, doneId);
          onImported?.(result);
          onClose();
        } catch (err) {
          setError(err.message);
          onError?.(err.message);
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
          <p className="muted">大分類固定為「{issueMajor?.name || "議題"}」。請指定處理中、加簽與已結案要對應的小分類。</p>
          <label>
            議題檔（xls 或 html）
            <input
              ref={fileRef}
              className="import-file-input"
              type="file"
              accept=".xls,.html,.htm"
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
          <label>
            處理中小分類
            <AppSelect
              colored
              value={inProgressId}
              placeholder="請選擇"
              options={[
                { value: "", label: "請選擇" },
                ...subs.map((s) => ({ value: s.id, label: s.name, color: s.color }))
              ]}
              onChange={(value) => {
                setError("");
                setInProgressId(value);
              }}
            />
          </label>
          <label>
            加簽小分類
            <AppSelect
              colored
              value={countersignId}
              placeholder="請選擇"
              options={[
                { value: "", label: "請選擇" },
                ...subs.map((s) => ({ value: s.id, label: s.name, color: s.color }))
              ]}
              onChange={(value) => {
                setError("");
                setCountersignId(value);
              }}
            />
          </label>
          <label>
            已結案小分類
            <AppSelect
              colored
              value={doneId}
              placeholder="請選擇"
              options={[
                { value: "", label: "請選擇" },
                ...subs.map((s) => ({ value: s.id, label: s.name, color: s.color }))
              ]}
              onChange={(value) => {
                setError("");
                setDoneId(value);
              }}
            />
          </label>
        </>
      )}
    </AppDialog>
  );
}
