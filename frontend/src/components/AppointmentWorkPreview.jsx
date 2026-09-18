import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import { api } from "../api.js";
import AppDialog from "./AppDialog.jsx";

function dash(value) {
  const text = String(value ?? "").trim();
  return text.length ? text : "—";
}

function formatOwner(project) {
  if (!project?.ownerMemberName) return "—";
  return project.ownerMemberEnglishName
    ? `${project.ownerMemberName}（${project.ownerMemberEnglishName}）`
    : project.ownerMemberName;
}

function TodoKids({ nodes }) {
  if (!nodes?.length) return null;
  return (
    <ul className="appointment-todo-kids">
      {nodes.map((node) => (
        <li key={node.id}>
          <span className={node.isCompleted ? "is-done" : ""}>{node.title}</span>
          {node.content ? <em className="muted"> {node.content}</em> : null}
          <TodoKids nodes={node.children} />
        </li>
      ))}
    </ul>
  );
}

function workHref(item) {
  if (item.kind === "issue" && item.issueId) return `/issues/${item.issueId}`;
  if (item.kind === "project" && item.projectId) return `/projects/${item.projectId}`;
  if (item.kind === "todo" && item.todoId && item.issueId) return `/issues/${item.issueId}?todo=${item.todoId}`;
  if (item.kind === "todo" && item.todoId && item.projectId) {
    return `/projects/${item.projectId}?tab=content&todo=${item.todoId}`;
  }
  return "";
}

export default function AppointmentWorkPreview({ item, onClose }) {
  const navigate = useNavigate();
  const [detail, setDetail] = useState(null);
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (!item) {
      setDetail(null);
      setError("");
      setLoading(false);
      return;
    }
    (async () => {
      setLoading(true);
      setError("");
      try {
        if (item.kind === "project" || (item.kind === "todo" && item.projectId)) {
          setDetail({
            type: "project",
            project: await api.project(item.projectId),
            todo: item.kind === "todo" ? item : null
          });
          return;
        }
        if (item.issueId) {
          setDetail({
            type: "issue",
            issue: await api.issue(item.issueId),
            todo: item.kind === "todo" ? item : null
          });
          return;
        }
        setDetail(null);
        setError("找不到對應工作");
      } catch (err) {
        setDetail(null);
        setError(err.message);
      } finally {
        setLoading(false);
      }
    })();
  }, [item]);

  const href = item ? workHref(item) : "";
  const title = item?.kind === "project"
    ? "專案詳情"
    : item?.kind === "todo"
      ? "待辦詳情"
      : "議題詳情";

  return (
    <AppDialog
      open={!!item}
      wide
      title={title}
      cancelLabel="關閉"
      confirmLabel={href ? "跳轉" : "關閉"}
      onCancel={onClose}
      onSubmit={() => {
        if (href) navigate(href);
        onClose();
      }}
    >
      {loading && <p className="muted">載入中…</p>}
      {error && <p className="banner error">{error}</p>}
      {detail?.todo && (
        <dl className="readonly-fields">
          <div className="readonly-span">
            <dt>待辦標題</dt>
            <dd>{dash(detail.todo.title)}</dd>
          </div>
          <div className="readonly-span">
            <dt>待辦內容</dt>
            <dd className="import-preview-text">{dash(detail.todo.content)}</dd>
          </div>
          {detail.todo.workLabel ? (
            <div className="readonly-span">
              <dt>所屬工作</dt>
              <dd>{detail.todo.workLabel}</dd>
            </div>
          ) : null}
          {detail.todo.children?.length > 0 && (
            <div className="readonly-span">
              <dt>子步驟</dt>
              <dd><TodoKids nodes={detail.todo.children} /></dd>
            </div>
          )}
        </dl>
      )}
      {detail?.type === "issue" && detail.issue && (
        <dl className="readonly-fields">
          <div><dt>編號</dt><dd>{dash(detail.issue.issueNo)}</dd></div>
          <div><dt>分類</dt><dd>{dash(detail.issue.subCategoryName || detail.issue.majorCategoryName)}</dd></div>
          <div className="readonly-span">
            <dt>標題</dt>
            <dd>{dash(detail.issue.title)}</dd>
          </div>
          <div className="readonly-span">
            <dt>內容</dt>
            <dd className="import-preview-text">{dash(detail.issue.content)}</dd>
          </div>
          <div><dt>客戶公司</dt><dd>{dash(detail.issue.clientCompanyName)}</dd></div>
          <div><dt>預計完成日</dt><dd>{dash(detail.issue.dueDate)}</dd></div>
          <div className="readonly-span">
            <dt>備註</dt>
            <dd className="import-preview-text">{dash(detail.issue.remark)}</dd>
          </div>
        </dl>
      )}
      {detail?.type === "project" && detail.project && (
        <dl className="readonly-fields">
          <div><dt>專案代碼</dt><dd>{dash(detail.project.code)}</dd></div>
          <div><dt>分類</dt><dd>{dash(detail.project.subCategoryName || detail.project.majorCategoryName)}</dd></div>
          <div className="readonly-span">
            <dt>專案名稱</dt>
            <dd>{dash(detail.project.name)}</dd>
          </div>
          <div className="readonly-span">
            <dt>說明</dt>
            <dd className="import-preview-text">{dash(detail.project.description)}</dd>
          </div>
          <div><dt>客戶公司</dt><dd>{dash(detail.project.clientCompanyName)}</dd></div>
          <div><dt>負責人</dt><dd>{formatOwner(detail.project)}</dd></div>
          <div><dt>開始日</dt><dd>{dash(detail.project.startDate)}</dd></div>
          <div><dt>預計完成日</dt><dd>{dash(detail.project.dueDate)}</dd></div>
        </dl>
      )}
    </AppDialog>
  );
}
