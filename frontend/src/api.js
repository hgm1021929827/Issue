function resolveApiBase() {
  const runtime = typeof window !== "undefined" ? window.__ISSUE_CONFIG__?.apiBase : undefined;
  if (typeof runtime === "string") {
    return runtime.replace(/\/$/, "");
  }
  const env = import.meta.env.VITE_API_BASE;
  if (typeof env === "string" && env.length > 0) {
    return env.replace(/\/$/, "");
  }
  return "http://localhost:5080";
}

const BASE = resolveApiBase();

async function request(path, options = {}) {
  const response = await fetch(`${BASE}${path}`, {
    headers: { "Content-Type": "application/json", ...(options.headers || {}) },
    ...options
  });
  const body = await response.json().catch(() => ({ code: response.status, message: "無法解析回應" }));
  if (body.code !== 200) {
    const err = new Error(body.message || "請求失敗");
    err.code = body.code;
    err.data = body.data;
    throw err;
  }
  return body.data;
}

async function requestForm(path, formData) {
  const response = await fetch(`${BASE}${path}`, { method: "POST", body: formData });
  const body = await response.json().catch(() => ({ code: response.status, message: "無法解析回應" }));
  if (body.code !== 200) {
    const err = new Error(body.message || "請求失敗");
    err.code = body.code;
    err.data = body.data;
    throw err;
  }
  return body.data;
}

export const api = {
  majorCategories: () => request("/majorCategories"),
  createMajor: (payload) => request("/majorCategories", { method: "POST", body: JSON.stringify(payload) }),
  updateMajor: (id, payload) => request(`/majorCategories/${id}`, { method: "PUT", body: JSON.stringify(payload) }),
  deleteMajor: (id) => request(`/majorCategories/${id}`, { method: "DELETE" }),
  subCategories: (majorCategoryId) =>
    request(majorCategoryId ? `/subCategories?majorCategoryId=${majorCategoryId}` : "/subCategories"),
  createSub: (payload) => request("/subCategories", { method: "POST", body: JSON.stringify(payload) }),
  updateSub: (id, payload) => request(`/subCategories/${id}`, { method: "PUT", body: JSON.stringify(payload) }),
  deleteSub: (id) => request(`/subCategories/${id}`, { method: "DELETE" }),
  colorPresets: () => request("/colorPresets"),
  issues: ({ majorCategoryId, subCategoryId, clientCompanyId, q } = {}) => {
    const params = new URLSearchParams();
    if (majorCategoryId) params.set("majorCategoryId", String(majorCategoryId));
    if (subCategoryId) params.set("subCategoryId", String(subCategoryId));
    if (clientCompanyId) params.set("clientCompanyId", String(clientCompanyId));
    if (q) params.set("q", q);
    const suffix = params.toString() ? `?${params}` : "";
    return request(`/issues${suffix}`);
  },
  calendar: (year, month) => request(`/issues/calendar?year=${year}&month=${month}`),
  addPlan: (id, date) => request(`/issues/${id}/plans`, { method: "POST", body: JSON.stringify({ date }) }),
  addPlans: (id, dates) => request(`/issues/${id}/plans`, { method: "POST", body: JSON.stringify({ dates }) }),
  removePlan: (id, date) => request(`/issues/${id}/plans?date=${date}`, { method: "DELETE" }),
  issue: (id) => request(`/issues/${id}`),
  createIssue: (payload) => request("/issues", { method: "POST", body: JSON.stringify(payload) }),
  updateIssue: (id, payload) => request(`/issues/${id}`, { method: "PUT", body: JSON.stringify(payload) }),
  deleteIssue: (id) => request(`/issues/${id}`, { method: "DELETE" }),
  todos: (issueId) => request(`/issues/${issueId}/todos`),
  createTodo: (issueId, payload) =>
    request(`/issues/${issueId}/todos`, { method: "POST", body: JSON.stringify(payload) }),
  updateTodo: (id, payload) => request(`/todos/${id}`, { method: "PUT", body: JSON.stringify(payload) }),
  completeTodo: (id, isCompleted) =>
    request(`/todos/${id}/complete`, { method: "PUT", body: JSON.stringify({ isCompleted }) }),
  reorderTodos: (id, payload) => request(`/todos/${id}/reorder`, { method: "PUT", body: JSON.stringify(payload) }),
  deleteTodo: (id) => request(`/todos/${id}`, { method: "DELETE" }),
  projects: (majorCategoryId) =>
    request(majorCategoryId ? `/projects?majorCategoryId=${majorCategoryId}` : "/projects"),
  project: (id) => request(`/projects/${id}`),
  createProject: (payload) => request("/projects", { method: "POST", body: JSON.stringify(payload) }),
  updateProject: (id, payload) => request(`/projects/${id}`, { method: "PUT", body: JSON.stringify(payload) }),
  deleteProject: (id) => request(`/projects/${id}`, { method: "DELETE" }),
  projectItems: (projectId) => request(`/projects/${projectId}/items`),
  createProjectItem: (projectId, payload) =>
    request(`/projects/${projectId}/items`, { method: "POST", body: JSON.stringify(payload) }),
  updateProjectItem: (projectId, id, payload) =>
    request(`/projects/${projectId}/items/${id}`, { method: "PUT", body: JSON.stringify(payload) }),
  deleteProjectItem: (projectId, id) =>
    request(`/projects/${projectId}/items/${id}`, { method: "DELETE" }),
  companyMembers: (q) => request(q ? `/company-members?q=${encodeURIComponent(q)}` : "/company-members"),
  createCompanyMember: (payload) => request("/company-members", { method: "POST", body: JSON.stringify(payload) }),
  updateCompanyMember: (id, payload) =>
    request(`/company-members/${id}`, { method: "PUT", body: JSON.stringify(payload) }),
  deleteCompanyMember: (id) => request(`/company-members/${id}`, { method: "DELETE" }),
  clientCompanies: (q) => request(q ? `/client-companies?q=${encodeURIComponent(q)}` : "/client-companies"),
  clientCompanyTree: (q) =>
    request(q ? `/client-companies/tree?q=${encodeURIComponent(q)}` : "/client-companies/tree"),
  createClientCompany: (payload) => request("/client-companies", { method: "POST", body: JSON.stringify(payload) }),
  updateClientCompany: (id, payload) =>
    request(`/client-companies/${id}`, { method: "PUT", body: JSON.stringify(payload) }),
  deleteClientCompany: (id) => request(`/client-companies/${id}`, { method: "DELETE" }),
  createClientContact: (companyId, payload) =>
    request(`/client-companies/${companyId}/contacts`, { method: "POST", body: JSON.stringify(payload) }),
  updateClientContact: (companyId, id, payload) =>
    request(`/client-companies/${companyId}/contacts/${id}`, { method: "PUT", body: JSON.stringify(payload) }),
  deleteClientContact: (companyId, id) =>
    request(`/client-companies/${companyId}/contacts/${id}`, { method: "DELETE" }),
  createContactChannel: (companyId, contactId, payload) =>
    request(`/client-companies/${companyId}/contacts/${contactId}/channels`, {
      method: "POST",
      body: JSON.stringify(payload)
    }),
  updateContactChannel: (companyId, contactId, channelId, payload) =>
    request(`/client-companies/${companyId}/contacts/${contactId}/channels/${channelId}`, {
      method: "PUT",
      body: JSON.stringify(payload)
    }),
  deleteContactChannel: (companyId, contactId, channelId) =>
    request(`/client-companies/${companyId}/contacts/${contactId}/channels/${channelId}`, { method: "DELETE" }),
  issueTrackTodos: (issueId) => request(`/issues/${issueId}/track-todos`),
  createIssueTrackTodo: (issueId, payload) =>
    request(`/issues/${issueId}/track-todos`, { method: "POST", body: JSON.stringify(payload) }),
  projectTrackTodos: (projectId) => request(`/projects/${projectId}/track-todos`),
  createProjectTrackTodo: (projectId, payload) =>
    request(`/projects/${projectId}/track-todos`, { method: "POST", body: JSON.stringify(payload) }),
  projectItemTrackTodos: (projectId, itemId) =>
    request(`/projects/${projectId}/items/${itemId}/track-todos`),
  createProjectItemTrackTodo: (projectId, itemId, payload) =>
    request(`/projects/${projectId}/items/${itemId}/track-todos`, { method: "POST", body: JSON.stringify(payload) }),
  homeTrackTodos: () => request("/track-todos"),
  updateTrackTodo: (id, payload) => request(`/track-todos/${id}`, { method: "PUT", body: JSON.stringify(payload) }),
  completeTrackTodo: (id, isCompleted) =>
    request(`/track-todos/${id}/complete`, { method: "PUT", body: JSON.stringify({ isCompleted }) }),
  reorderTrackTodos: (id, orderedIds) =>
    request(`/track-todos/${id}/reorder`, { method: "PUT", body: JSON.stringify({ orderedIds }) }),
  deleteTrackTodo: (id) => request(`/track-todos/${id}`, { method: "DELETE" }),
  clientContacts: (companyId) => request(`/client-companies/${companyId}/contacts`),
  projectWorkItems: (projectId) => request(`/projects/${projectId}/work-items`),
  projectWorkItem: (projectId, id) => request(`/projects/${projectId}/work-items/${id}`),
  completeProjectWorkItem: (projectId, id, isCompleted) =>
    request(`/projects/${projectId}/work-items/${id}/complete`, {
      method: "PUT",
      body: JSON.stringify({ isCompleted })
    }),
  updateWorkItemRemark: (projectId, id, remark) =>
    request(`/projects/${projectId}/work-items/${id}/remark`, {
      method: "PUT",
      body: JSON.stringify({ remark })
    }),
  updateItemRemark: (projectId, id, remark) =>
    request(`/projects/${projectId}/items/${id}/remark`, {
      method: "PUT",
      body: JSON.stringify({ remark })
    }),
  deleteProjectWorkItem: (projectId, id) =>
    request(`/projects/${projectId}/work-items/${id}`, { method: "DELETE" }),
  workItemHours: (projectId, id) => request(`/projects/${projectId}/work-items/${id}/hours`),
  createWorkItemHour: (projectId, id, payload) =>
    request(`/projects/${projectId}/work-items/${id}/hours`, { method: "POST", body: JSON.stringify(payload) }),
  updateWorkItemHour: (projectId, id, hourId, payload) =>
    request(`/projects/${projectId}/work-items/${id}/hours/${hourId}`, {
      method: "PUT",
      body: JSON.stringify(payload)
    }),
  deleteWorkItemHour: (projectId, id, hourId) =>
    request(`/projects/${projectId}/work-items/${id}/hours/${hourId}`, { method: "DELETE" }),
  workItemTrackTodos: (projectId, id) => request(`/projects/${projectId}/work-items/${id}/track-todos`),
  createWorkItemTrackTodo: (projectId, id, payload) =>
    request(`/projects/${projectId}/work-items/${id}/track-todos`, {
      method: "POST",
      body: JSON.stringify(payload)
    }),
  completeProjectItem: (projectId, id, isCompleted) =>
    request(`/projects/${projectId}/items/${id}/complete`, {
      method: "PUT",
      body: JSON.stringify({ isCompleted })
    }),
  itemHours: (projectId, itemId) => request(`/projects/${projectId}/items/${itemId}/hours`),
  createItemHour: (projectId, itemId, payload) =>
    request(`/projects/${projectId}/items/${itemId}/hours`, { method: "POST", body: JSON.stringify(payload) }),
  updateItemHour: (projectId, itemId, hourId, payload) =>
    request(`/projects/${projectId}/items/${itemId}/hours/${hourId}`, {
      method: "PUT",
      body: JSON.stringify(payload)
    }),
  deleteItemHour: (projectId, itemId, hourId) =>
    request(`/projects/${projectId}/items/${itemId}/hours/${hourId}`, { method: "DELETE" }),
  resolveProjectImport: (file) => {
    const form = new FormData();
    form.append("file", file);
    return requestForm("/project-imports/resolve-project", form);
  },
  importProjectExcel: (projectId, file, currentUserMemberId) => {
    const form = new FormData();
    form.append("file", file);
    form.append("currentUserMemberId", String(currentUserMemberId));
    return requestForm(`/projects/${projectId}/imports`, form);
  },
  applyImportDecisions: (projectId, decisions) =>
    request(`/projects/${projectId}/import-decisions`, {
      method: "POST",
      body: JSON.stringify({ decisions })
    }),
  importIssues: (file, currentUserMemberId, inProgressSubCategoryId, countersignSubCategoryId, doneSubCategoryId) => {
    const form = new FormData();
    form.append("file", file);
    form.append("currentUserMemberId", String(currentUserMemberId));
    form.append("inProgressSubCategoryId", String(inProgressSubCategoryId));
    form.append("countersignSubCategoryId", String(countersignSubCategoryId));
    form.append("doneSubCategoryId", String(doneSubCategoryId));
    return requestForm("/issue-imports", form);
  },
  applyIssueImportDecisions: (decisions, categoryChoices = [], extras = {}) =>
    request("/issue-imports/decisions", {
      method: "POST",
      body: JSON.stringify({
        decisions,
        categoryChoices,
        countersignSubCategoryId: extras.countersignSubCategoryId || undefined,
        doneSubCategoryId: extras.doneSubCategoryId || undefined
      })
    })
};
