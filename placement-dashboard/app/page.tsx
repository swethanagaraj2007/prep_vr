'use client';

import React, { useEffect, useState, useCallback } from 'react';
import { useAuth } from '../lib/auth-context';
import { LoginPage } from '../components/LoginPage';
import { Navbar, NavTabType } from '../components/Navbar';
import { OverviewView } from '../components/OverviewView';
import { StudentDirectoryView } from '../components/StudentDirectoryView';
import { StudentRankingTable } from '../components/StudentRankingTable';
import { SessionsView } from '../components/SessionsView';
import { ReportsView } from '../components/ReportsView';
import { StudentDetailsModal } from '../components/StudentDetailsModal';
import { RegisterStudentModal } from '../components/RegisterStudentModal';
import { ImportCsvModal } from '../components/ImportCsvModal';
import { EditStudentModal } from '../components/EditStudentModal';
import { DashboardSummary, StudentRanking, DetailedStudent, getDashboardSummary, getRankings } from '../lib/api';
import { Activity } from 'lucide-react';

export default function DashboardPage() {
  const { user, isLoading: authLoading } = useAuth();

  const [activeTab, setActiveTab] = useState<NavTabType>('overview');
  const [summary, setSummary] = useState<DashboardSummary | null>(null);
  const [students, setStudents] = useState<StudentRanking[]>([]);
  const [dataLoading, setDataLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [refreshTrigger, setRefreshTrigger] = useState(0);

  // Modals state
  const [selectedStudentId, setSelectedStudentId] = useState<string | null>(null);
  const [showRegisterModal, setShowRegisterModal] = useState(false);
  const [showImportCsvModal, setShowImportCsvModal] = useState(false);
  const [editingStudent, setEditingStudent] = useState<DetailedStudent | null>(null);

  const loadData = useCallback(async (isBackground = false) => {
    try {
      if (!isBackground) setRefreshing(true);
      const [sumData, rankData] = await Promise.all([
        getDashboardSummary(),
        getRankings(),
      ]);
      setSummary(sumData);
      setStudents(rankData);
      setRefreshTrigger((prev) => prev + 1);
    } catch (err) {
      console.error('Failed to load dashboard data:', err);
    } finally {
      setDataLoading(false);
      if (!isBackground) setRefreshing(false);
    }
  }, []);

  useEffect(() => {
    if (user) {
      loadData(false);
      // Auto-poll every 4 seconds for real-time interview sync from Unity
      const interval = setInterval(() => {
        loadData(true);
      }, 4000);
      return () => clearInterval(interval);
    }
  }, [user, loadData]);

  if (authLoading) {
    return (
      <div style={{ minHeight: '100vh', display: 'flex', alignItems: 'center', justifyContent: 'center', background: '#090d16' }}>
        <Activity size={32} className="spin" color="#38bdf8" />
      </div>
    );
  }

  if (!user) {
    return <LoginPage />;
  }

  return (
    <div style={{ minHeight: '100vh', display: 'flex', flexDirection: 'column', background: 'radial-gradient(ellipse at top, #111e38 0%, #080d1a 100%)' }}>
      {/* Top Navbar with Navigation Tabs */}
      <Navbar
        activeTab={activeTab}
        onSelectTab={setActiveTab}
        onRefresh={loadData}
        isRefreshing={refreshing}
      />

      {/* Main Tab Content */}
      <main style={{ flex: 1 }}>
        {activeTab === 'overview' && (
          <OverviewView
            summary={summary}
            rankings={students}
            isLoading={dataLoading}
            onSelectStudent={(rollOrId) => setSelectedStudentId(rollOrId)}
            onNavigateTab={(tab) => setActiveTab(tab)}
          />
        )}

        {activeTab === 'students' && (
          <StudentDirectoryView
            onSelectStudent={(rollOrId) => setSelectedStudentId(rollOrId)}
            onOpenRegisterModal={() => setShowRegisterModal(true)}
            onOpenImportCsvModal={() => setShowImportCsvModal(true)}
            onOpenEditModal={(student) => setEditingStudent(student)}
            refreshTrigger={refreshTrigger}
          />
        )}

        {activeTab === 'rankings' && (
          <StudentRankingTable
            students={students}
            isLoading={dataLoading}
            onSelectStudent={(rollOrId) => setSelectedStudentId(rollOrId)}
            onOpenRegisterModal={() => setShowRegisterModal(true)}
            onRefresh={loadData}
          />
        )}

        {activeTab === 'sessions' && (
          <SessionsView
            onSelectStudent={(rollOrId) => setSelectedStudentId(rollOrId)}
            refreshTrigger={refreshTrigger}
          />
        )}

        {activeTab === 'reports' && (
          <ReportsView summary={summary} />
        )}
      </main>

      {/* Modal: Student Detailed Profile & Feedback */}
      <StudentDetailsModal
        studentIdOrRoll={selectedStudentId}
        onClose={() => setSelectedStudentId(null)}
      />

      {/* Modal: Register Student */}
      <RegisterStudentModal
        isOpen={showRegisterModal}
        onClose={() => setShowRegisterModal(false)}
        onSuccess={loadData}
      />

      {/* Modal: Import CSV */}
      <ImportCsvModal
        isOpen={showImportCsvModal}
        onClose={() => setShowImportCsvModal(false)}
        onSuccess={loadData}
      />

      {/* Modal: Edit Student */}
      <EditStudentModal
        student={editingStudent}
        isOpen={!!editingStudent}
        onClose={() => setEditingStudent(null)}
        onSuccess={loadData}
      />

      {/* Footer */}
      <footer style={{ padding: '20px 24px', textAlign: 'center', fontSize: '0.8rem', color: '#64748b', borderTop: '1px solid var(--border-color)' }}>
        PREP VR — AI-Powered Placement Interview Simulation & Institutional Analytics Platform &copy; 2026. All rights reserved.
      </footer>
    </div>
  );
}
