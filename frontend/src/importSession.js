const MEMBER_KEY = "issue.importMemberId";
const ISSUE_MEMBER_KEY = "issue.issueImportMemberId";

let pending = null;

export function getRememberedImportMemberId() {
  return window.localStorage.getItem(MEMBER_KEY) || "";
}

export function rememberImportMemberId(id) {
  if (id) window.localStorage.setItem(MEMBER_KEY, String(id));
}

export function getRememberedIssueImportMemberId() {
  return window.localStorage.getItem(ISSUE_MEMBER_KEY) || "";
}

export function rememberIssueImportMemberId(id) {
  if (id) window.localStorage.setItem(ISSUE_MEMBER_KEY, String(id));
}

const IN_PROGRESS_SUB_KEY = "issue.issueImportInProgressSubId";
const COUNTERSIGN_SUB_KEY = "issue.issueImportCountersignSubId";
const DONE_SUB_KEY = "issue.issueImportDoneSubId";

export function getRememberedIssueImportInProgressSubId() {
  return window.localStorage.getItem(IN_PROGRESS_SUB_KEY) || "";
}

export function rememberIssueImportInProgressSubId(id) {
  if (id) window.localStorage.setItem(IN_PROGRESS_SUB_KEY, String(id));
}

export function getRememberedIssueImportCountersignSubId() {
  return window.localStorage.getItem(COUNTERSIGN_SUB_KEY) || "";
}

export function rememberIssueImportCountersignSubId(id) {
  if (id) window.localStorage.setItem(COUNTERSIGN_SUB_KEY, String(id));
}

export function getRememberedIssueImportDoneSubId() {
  return window.localStorage.getItem(DONE_SUB_KEY) || "";
}

export function rememberIssueImportDoneSubId(id) {
  if (id) window.localStorage.setItem(DONE_SUB_KEY, String(id));
}

export function setPendingImport(value) {
  pending = value;
}

export function peekPendingImport() {
  return pending;
}

export function takePendingImport() {
  const value = pending;
  pending = null;
  return value;
}
