import { NavLink, Route, Routes } from "react-router-dom";
import { ClipboardList, FolderCog, FolderKanban, Home, Sparkles, Users } from "lucide-react";
import HomePage from "./pages/HomePage.jsx";
import IssueListPage from "./pages/IssueListPage.jsx";
import IssuePage from "./pages/IssuePage.jsx";
import IssueImportResultPage from "./pages/IssueImportResultPage.jsx";
import CategoryPage from "./pages/CategoryPage.jsx";
import ProjectListPage from "./pages/ProjectListPage.jsx";
import ProjectPage from "./pages/ProjectPage.jsx";
import ImportResultPage from "./pages/ImportResultPage.jsx";
import MembersPage from "./pages/MembersPage.jsx";

export default function App() {
  return (
    <div className="app">
      <header className="topbar">
        <div className="brand">
          <Sparkles size={18} strokeWidth={1.75} className="brand-icon" aria-hidden="true" />
          <strong>工作追蹤</strong>
        </div>
        <nav>
          <NavLink to="/" end>
            <Home size={16} strokeWidth={1.75} aria-hidden="true" />
            首頁
          </NavLink>
          <NavLink to="/issues">
            <ClipboardList size={16} strokeWidth={1.75} aria-hidden="true" />
            議題
          </NavLink>
          <NavLink to="/projects">
            <FolderKanban size={16} strokeWidth={1.75} aria-hidden="true" />
            專案
          </NavLink>
          <NavLink to="/members">
            <Users size={16} strokeWidth={1.75} aria-hidden="true" />
            成員
          </NavLink>
          <NavLink to="/categories">
            <FolderCog size={16} strokeWidth={1.75} aria-hidden="true" />
            分類設定
          </NavLink>
        </nav>
      </header>
      <main>
        <Routes>
          <Route path="/" element={<HomePage />} />
          <Route path="/members" element={<MembersPage />} />
          <Route path="/categories" element={<CategoryPage />} />
          <Route path="/issues" element={<IssueListPage />} />
          <Route path="/issues/new" element={<IssuePage />} />
          <Route path="/issues/import-result" element={<IssueImportResultPage />} />
          <Route path="/issues/:id" element={<IssuePage />} />
          <Route path="/projects" element={<ProjectListPage />} />
          <Route path="/projects/new" element={<ProjectPage />} />
          <Route path="/projects/:id/import-result" element={<ImportResultPage />} />
          <Route path="/projects/:id" element={<ProjectPage />} />
        </Routes>
      </main>
    </div>
  );
}
