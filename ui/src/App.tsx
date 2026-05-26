import { Routes, Route, Navigate } from "react-router-dom";
import Layout from "./components/Layout";
import ApplicationsList from "./pages/ApplicationsList";
import ApplicationForm from "./pages/ApplicationForm";
import ApplicationDetail from "./pages/ApplicationDetail";
import RunHistory from "./pages/RunHistory";
import RunDetail from "./pages/RunDetail";

export default function App() {
  return (
    <Routes>
      <Route element={<Layout />}>
        <Route index element={<ApplicationsList />} />
        <Route path="applications/new" element={<ApplicationForm mode="create" />} />
        <Route path="applications/:id" element={<ApplicationDetail />} />
        <Route path="applications/:id/edit" element={<ApplicationForm mode="edit" />} />
        <Route path="runs" element={<RunHistory />} />
        <Route path="runs/:id" element={<RunDetail />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Route>
    </Routes>
  );
}
