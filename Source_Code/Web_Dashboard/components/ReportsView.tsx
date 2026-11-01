'use client';

import React, { useState } from 'react';
import { getExportCsvUrl, DashboardSummary } from '../lib/api';
import {
  FileSpreadsheet,
  Download,
  Award,
  Users,
  Video,
  BarChart3,
  CheckCircle,
  FileText
} from 'lucide-react';

interface ReportsViewProps {
  summary: DashboardSummary | null;
}

export const ReportsView: React.FC<ReportsViewProps> = ({ summary }) => {
  const [selectedDept, setSelectedDept] = useState('All');
  const [downloading, setDownloading] = useState<string | null>(null);

  const handleDownload = (type: 'rankings' | 'students' | 'sessions' | 'department') => {
    setDownloading(type);
    const url = getExportCsvUrl(type, { department: selectedDept });
    window.open(url, '_blank');
    setTimeout(() => setDownloading(null), 1500);
  };

  return (
    <div style={{ padding: '0 24px 32px 24px' }}>
      {/* Banner */}
      <div
        className="glass-panel"
        style={{
          padding: '24px',
          marginBottom: '24px',
          background: 'linear-gradient(135deg, rgba(15, 23, 42, 0.9) 0%, rgba(2, 132, 199, 0.12) 100%)',
          border: '1px solid rgba(56, 189, 248, 0.3)',
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          flexWrap: 'wrap',
          gap: '16px',
        }}
      >
        <div>
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px', marginBottom: '6px' }}>
            <FileSpreadsheet size={24} color="#38bdf8" />
            <h2 style={{ fontSize: '1.35rem', fontWeight: 700, color: '#f8fafc' }}>
              Institutional Placement Reports & Export Center
            </h2>
          </div>
          <p style={{ fontSize: '0.85rem', color: '#94a3b8', maxWidth: '650px' }}>
            Generate and export official placement audits, departmental readiness summaries, and ranked student merit lists directly from live VR interview evaluation data.
          </p>
        </div>

        <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
          <span style={{ fontSize: '0.8rem', color: '#94a3b8' }}>Filter by Dept:</span>
          <select
            value={selectedDept}
            onChange={(e) => setSelectedDept(e.target.value)}
            style={{
              padding: '8px 14px',
              borderRadius: '8px',
              background: 'rgba(15, 23, 42, 0.8)',
              border: '1px solid rgba(255, 255, 255, 0.15)',
              color: '#f8fafc',
              fontSize: '0.85rem',
            }}
          >
            <option value="All">All Departments</option>
            <option value="Computer Science & Engineering">CSE</option>
            <option value="Information Technology">IT</option>
            <option value="AI & Data Science">AI & DS</option>
            <option value="Electronics & Communication">ECE</option>
            <option value="Mechanical Engineering">Mech</option>
          </select>
        </div>
      </div>

      {/* Report Cards Grid */}
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(320px, 1fr))', gap: '20px', marginBottom: '28px' }}>
        {/* Report 1: Student Rankings */}
        <div className="glass-card" style={{ padding: '24px', display: 'flex', flexDirection: 'column', justifyContent: 'space-between' }}>
          <div>
            <div style={{ width: '44px', height: '44px', borderRadius: '12px', background: 'rgba(2, 132, 199, 0.15)', border: '1px solid rgba(56, 189, 248, 0.3)', display: 'flex', alignItems: 'center', justifyContent: 'center', marginBottom: '16px' }}>
              <Award size={22} color="#38bdf8" />
            </div>
            <h3 style={{ fontSize: '1.1rem', fontWeight: 600, color: '#f8fafc', marginBottom: '8px' }}>
              Student Merit & Ranking Report
            </h3>
            <p style={{ fontSize: '0.82rem', color: '#94a3b8', lineHeight: 1.5, marginBottom: '16px' }}>
              Official ranked list of candidates computed strictly from completed VR mock interviews, featuring multi-competency sub-scores and overall readiness indices.
            </p>
          </div>
          <button
            onClick={() => handleDownload('rankings')}
            disabled={downloading === 'rankings'}
            className="btn-primary"
            style={{ width: '100%', justifyContent: 'center' }}
          >
            <Download size={16} />
            {downloading === 'rankings' ? 'Generating...' : 'Export Rankings CSV'}
          </button>
        </div>

        {/* Report 2: Department-wise Performance */}
        <div className="glass-card" style={{ padding: '24px', display: 'flex', flexDirection: 'column', justifyContent: 'space-between' }}>
          <div>
            <div style={{ width: '44px', height: '44px', borderRadius: '12px', background: 'rgba(16, 185, 129, 0.15)', border: '1px solid rgba(16, 185, 129, 0.3)', display: 'flex', alignItems: 'center', justifyContent: 'center', marginBottom: '16px' }}>
              <BarChart3 size={22} color="#34d399" />
            </div>
            <h3 style={{ fontSize: '1.1rem', fontWeight: 600, color: '#f8fafc', marginBottom: '8px' }}>
              Departmental Analytics & Readiness
            </h3>
            <p style={{ fontSize: '0.82rem', color: '#94a3b8', lineHeight: 1.5, marginBottom: '16px' }}>
              Comparative breakdown per department covering total candidate headcount, mock interview completion percentages, and average readiness benchmarks.
            </p>
          </div>
          <button
            onClick={() => handleDownload('department')}
            disabled={downloading === 'department'}
            className="btn-primary"
            style={{ width: '100%', justifyContent: 'center', background: 'linear-gradient(135deg, #059669 0%, #047857 100%)', borderColor: '#34d399' }}
          >
            <Download size={16} />
            {downloading === 'department' ? 'Generating...' : 'Export Dept Analytics CSV'}
          </button>
        </div>

        {/* Report 3: Interview Sessions Audit */}
        <div className="glass-card" style={{ padding: '24px', display: 'flex', flexDirection: 'column', justifyContent: 'space-between' }}>
          <div>
            <div style={{ width: '44px', height: '44px', borderRadius: '12px', background: 'rgba(245, 158, 11, 0.15)', border: '1px solid rgba(245, 158, 11, 0.3)', display: 'flex', alignItems: 'center', justifyContent: 'center', marginBottom: '16px' }}>
              <Video size={22} color="#fbbf24" />
            </div>
            <h3 style={{ fontSize: '1.1rem', fontWeight: 600, color: '#f8fafc', marginBottom: '8px' }}>
              Interview Sessions Audit Log
            </h3>
            <p style={{ fontSize: '0.82rem', color: '#94a3b8', lineHeight: 1.5, marginBottom: '16px' }}>
              Comprehensive timestamped record of every VR mock interview session, duration, stage completion count, and error/interrupted states.
            </p>
          </div>
          <button
            onClick={() => handleDownload('sessions')}
            disabled={downloading === 'sessions'}
            className="btn-primary"
            style={{ width: '100%', justifyContent: 'center', background: 'linear-gradient(135deg, #d97706 0%, #b45309 100%)', borderColor: '#fbbf24' }}
          >
            <Download size={16} />
            {downloading === 'sessions' ? 'Generating...' : 'Export Sessions Log CSV'}
          </button>
        </div>

        {/* Report 4: Student Cohort Master Directory */}
        <div className="glass-card" style={{ padding: '24px', display: 'flex', flexDirection: 'column', justifyContent: 'space-between' }}>
          <div>
            <div style={{ width: '44px', height: '44px', borderRadius: '12px', background: 'rgba(147, 51, 234, 0.15)', border: '1px solid rgba(168, 85, 247, 0.3)', display: 'flex', alignItems: 'center', justifyContent: 'center', marginBottom: '16px' }}>
              <Users size={22} color="#c084fc" />
            </div>
            <h3 style={{ fontSize: '1.1rem', fontWeight: 600, color: '#f8fafc', marginBottom: '8px' }}>
              Student Cohort Master Directory
            </h3>
            <p style={{ fontSize: '0.82rem', color: '#94a3b8', lineHeight: 1.5, marginBottom: '16px' }}>
              Full directory of registered candidates, contact emails, departments, graduation batches, and current interview status.
            </p>
          </div>
          <button
            onClick={() => handleDownload('students')}
            disabled={downloading === 'students'}
            className="btn-primary"
            style={{ width: '100%', justifyContent: 'center', background: 'linear-gradient(135deg, #7e22ce 0%, #6b21a8 100%)', borderColor: '#c084fc' }}
          >
            <Download size={16} />
            {downloading === 'students' ? 'Generating...' : 'Export Cohort Directory CSV'}
          </button>
        </div>
      </div>

      {/* Summary Table: Department-wise Snapshot */}
      {summary && summary.departmentBreakdown && summary.departmentBreakdown.length > 0 && (
        <div className="glass-panel" style={{ overflow: 'hidden' }}>
          <div
            style={{
              padding: '16px 20px',
              borderBottom: '1px solid var(--border-color)',
              background: 'rgba(15, 23, 42, 0.6)',
            }}
          >
            <h3 style={{ fontSize: '1rem', fontWeight: 600, color: '#f8fafc' }}>
              Department-Wise Readiness Snapshot
            </h3>
          </div>
          <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '0.85rem' }}>
            <thead>
              <tr style={{ borderBottom: '1px solid var(--border-color)', background: 'rgba(15, 23, 42, 0.4)', color: '#94a3b8' }}>
                <th style={{ padding: '12px 18px', fontWeight: 600 }}>Department</th>
                <th style={{ padding: '12px 18px', fontWeight: 600, textAlign: 'center' }}>Total Registered</th>
                <th style={{ padding: '12px 18px', fontWeight: 600, textAlign: 'center' }}>Completed VR Mock</th>
                <th style={{ padding: '12px 18px', fontWeight: 600, textAlign: 'center' }}>Completion Rate</th>
                <th style={{ padding: '12px 18px', fontWeight: 600, textAlign: 'center' }}>Avg Score</th>
                <th style={{ padding: '12px 18px', fontWeight: 600, textAlign: 'center' }}>Avg Readiness</th>
              </tr>
            </thead>
            <tbody>
              {summary.departmentBreakdown.map((dept, idx) => {
                const completionRate = dept.totalStudents > 0 ? Math.round((dept.completedInterviews / dept.totalStudents) * 100) : 0;
                return (
                  <tr key={idx} style={{ borderBottom: '1px solid rgba(255, 255, 255, 0.04)' }}>
                    <td style={{ padding: '12px 18px', fontWeight: 600, color: '#f1f5f9' }}>{dept.department}</td>
                    <td style={{ padding: '12px 18px', textAlign: 'center', color: '#cbd5e1' }}>{dept.totalStudents}</td>
                    <td style={{ padding: '12px 18px', textAlign: 'center', color: '#34d399', fontWeight: 600 }}>{dept.completedInterviews}</td>
                    <td style={{ padding: '12px 18px', textAlign: 'center' }}>
                      <span className="badge" style={{ background: 'rgba(56, 189, 248, 0.1)', color: '#38bdf8' }}>
                        {completionRate}%
                      </span>
                    </td>
                    <td style={{ padding: '12px 18px', textAlign: 'center', color: '#38bdf8', fontWeight: 600 }}>{dept.averageScore}</td>
                    <td style={{ padding: '12px 18px', textAlign: 'center', color: '#34d399', fontWeight: 700 }}>{dept.averageReadiness}%</td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
};
