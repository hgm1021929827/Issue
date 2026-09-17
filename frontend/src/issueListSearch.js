const KEY = "issueListSearch";

function normalizeSearch(search) {
  if (!search || search === "?") return "";
  return search.startsWith("?") ? search : `?${search}`;
}

export function rememberIssueListSearch(search) {
  try {
    sessionStorage.setItem(KEY, normalizeSearch(search));
  } catch {
    /* ignore quota / private mode */
  }
}

export function getIssueListPath() {
  let search = "";
  try {
    search = sessionStorage.getItem(KEY) || "";
  } catch {
    search = "";
  }
  return `/issues${normalizeSearch(search)}`;
}
