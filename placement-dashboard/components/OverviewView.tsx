'use client';

import React from 'react';
import { MetricsOverview } from './MetricsOverview';
import { DashboardSummary, StudentRanking } from '../lib/api';
import { Award, CheckCircle2, ChevronRight, TrendingUp, Users, Clock, PlayCircle } from 'lucide-react';

interface OverviewViewProps {
  summary: DashboardSummary | null;
  rankings: StudentRanking[];
  isLoading: boolean;
  onSelectStudent: (rollOrId: string) => void;
  onNavigateTab: (tab: any) => void;
}

export const OverviewView: React.FC<OverviewViewProps> = ({
  summary,
  rankings,
  isLoading,
  onSelectStudent,
  onNavigateTab,
}) => {
  // Top 3 ranked performers
  const topPerformers = rankings.filter((r) => r.rank > 0).slice(0, 3);

  // Recent 5 interviews
  const recentInterviews = rankings
    .filter((r) => r.interviewDate)
    .sort((a, b) => new Date(b.interviewDate!).getTime() - new Date(a.interviewDate!).getTime())
    .slice(0, 5);

  return (
    <div>
      {/* 1. Statistics Cards */}
      <MetricsOverview summary={summary} isLoading={isLoading} />

      {/* 2. Top Performers & Department Progress */}
      <div
        style={{
          display: 'grid',
          gridTemplateColumns: 'repeat(auto-fit, minmax(360px, 1fr))',
          gap: '20px',
          margin: '0 24px 24px 24px',
        }}
      >
        {/* Top Performers Card */}
        <div className="glass-panel" style={{ padding: '20px', display: 'flex', flexDirection: 'column' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
              <Award size={18} color="#38bdf8" />
              <h3 style={{ fontSize: '1rem', fontWeight: 600, color: '#f8fafc' }}>
                Top-Performing Candidates
              </h3>
            </div>
            <button
              onClick={() => onNavigateTab('rankings')}
              style={{ background: 'none', border: 'none', color: '#38bdf8', fontSize: '0.8rem', cursor: 'pointer', display: 'flex', alignItems: 'center', gap: '2px' }}
            >
              View Full Leaderboard <ChevronRight size={14} />
            </button>
          </div>

          <div style={{ display: 'flex', flexDirection: 'column', gap: '10px', flex: 1 }}>
            {topPerformers.length === 0 ? (
              <div style={{ padding: '20px', textAlign: 'center', color: '#64748b', fontSize: '0.85rem' }}>
                No completed evaluations recorded yet.
              </div>
            ) : (
              topPerformers.map((st) => (
                <div
                  key={st.studentId}
                  onClick={() => onSelectStudent(st.rollNumber)}
                  className="glass-card"
                  style={{
                    padding: '12px 16px',
                    display: 'flex',
                    justifyContent: 'space-between',
                    alignItems: 'center',
                    cursor: 'pointer',
                  }}
                >
                  <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
                    <div
                      style={{
                        width: '28px',
                        height: '28px',
                        borderRadius: '6px',
                        background:
                          st.rank === 1
                            ? 'linear-gradient(135deg, #f59e0b, #d97706)'
                            : st.rank === 2
                            ? 'linear-gradient(135deg, #94a3b8, #64748b)'
                            : 'linear-gradient(135deg, #b45309, #78350f)',
                        color: '#fff',
                        fontWeight: 700,
                        fontSize: '0.8rem',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                      }}
                    >
                      #{st.rank}
                    </div>
                    <div>
                      <div style={{ fontWeight: 600, color: '#f1f5f9', fontSize: '0.875rem' }}>
                        {st.studentName}
                      </div>
                      <div style={{ fontSize: '0.75rem', color: '#94a3b8' }}>
                        {st.rollNumber} &bull; {st.department}
                      </div>
                    </div>
                  </div>

                  <div style={{ textAlign: 'right' }}>
                    <div style={{ fontWeight: 700, color: '#34d399', fontSize: '0.95rem' }}>
                      {st.readinessScore}%
                    </div>
                    <div style={{ fontSize: '0.7rem', color: '#64748b' }}>
                      Score: {st.overallScore}
                    </div>
                  </div>
                </div>
              ))
            )}
          </div>
        </div>

        {/* Department Breakdown */}
        <div className="glass-panel" style={{ padding: '20px', display: 'flex', flexDirection: 'column' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '16px' }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
              <TrendingUp size={18} color="#34d399" />
              <h3 style={{ fontSize: '1rem', fontWeight: 600, color: '#f8fafc' }}>
                Department Placement Readiness
              </h3>
            </div>
            <button
              onClick={() => onNavigateTab('reports')}
              style={{ background: 'none', border: 'none', color: '#38bdf8', fontSize: '0.8rem', cursor: 'pointer', display: 'flex', alignItems: 'center', gap: '2px' }}
            >
              Export Report <ChevronRight size={14} />
            </button>
          </div>

          <div style={{ display: 'flex', flexDirection: 'column', gap: '14px', flex: 1, justifyContent: 'center' }}>
            {(!summary?.departmentBreakdown || summary.departmentBreakdown.length === 0) ? (
              <div style={{ padding: '20px', textAlign: 'center', color: '#64748b', fontSize: '0.85rem' }}>
                No department records available.
              </div>
            ) : (
              summary.departmentBreakdown.map((dept, idx) => (
                <div key={idx}>
                  <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '0.82rem', marginBottom: '4px' }}>
                    <span style={{ color: '#cbd5e1', fontWeight: 500 }}>{dept.department}</span>
                    <span style={{ color: '#38bdf8', fontWeight: 600 }}>
                      {dept.averageReadiness}% avg ({dept.completedInterviews}/{dept.totalStudents} tested)
                    </span>
                  </div>
                  <div style={{ width: '100%', height: '7px', background: 'rgba(255, 255, 255, 0.08)', borderRadius: '4px', overflow: 'hidden' }}>
                    <div
                      style={{
                        width: `${dept.averageReadiness}%`,
                        height: '100%',
                        background: 'linear-gradient(90deg, #0284c7 0%, #34d399 100%)',
                      }}
                    />
                  </div>
                </div>
              ))
            )}
          </div>
        </div>
      </div>

      {/* 3. Recent Interview Results */}
      <div className="glass-panel" style={{ margin: '0 24px 32px 24px', overflow: 'hidden' }}>
        <div
          style={{
            padding: '16px 20px',
            borderBottom: '1px solid var(--border-color)',
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
            background: 'rgba(15, 23, 42, 0.6)',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
            <Clock size={18} color="#38bdf8" />
            <h3 style={{ fontSize: '1rem', fontWeight: 600, color: '#f8fafc' }}>
              Recently Completed VR Interview Evaluations
            </h3>
          </div>
          <button
            onClick={() => onNavigateTab('sessions')}
            style={{ background: 'none', border: 'none', color: '#38bdf8', fontSize: '0.8rem', cursor: 'pointer', display: 'flex', alignItems: 'center', gap: '2px' }}
          >
            View All Sessions <ChevronRight size={14} />
          </button>
        </div>

        <div style={{ overflowX: 'auto' }}>
          <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '0.85rem' }}>
            <thead>
              <tr style={{ borderBottom: '1px solid var(--border-color)', background: 'rgba(15, 23, 42, 0.4)', color: '#94a3b8' }}>
                <th style={{ padding: '12px 18px', fontWeight: 600 }}>Candidate</th>
                <th style={{ padding: '12px 18px', fontWeight: 600 }}>Roll Number</th>
                <th style={{ padding: '12px 18px', fontWeight: 600 }}>Department</th>
                <th style={{ padding: '12px 18px', fontWeight: 600 }}>Date</th>
                <th style={{ padding: '12px 18px', fontWeight: 600, textAlign: 'center' }}>Readiness</th>
                <th style={{ padding: '12px 18px', fontWeight: 600, textAlign: 'center' }}>Overall Score</th>
                <th style={{ padding: '12px 18px', fontWeight: 600, textAlign: 'right' }}>Feedback</th>
              </tr>
            </thead>
            <tbody>
              {recentInterviews.length === 0 ? (
                <tr>
                  <td colSpan={7} style={{ padding: '30px', textAlign: 'center', color: '#64748b' }}>
                    No recent interview sessions available.
                  </td>
                </tr>
              ) : (
                recentInterviews.map((item) => (
                  <tr
                    key={item.studentId}
                    style={{ borderBottom: '1px solid rgba(255, 255, 255, 0.04)' }}
                  >
                    <td style={{ padding: '12px 18px', fontWeight: 600, color: '#f1f5f9' }}>
                      {item.studentName}
                    </td>
                    <td style={{ padding: '12px 18px' }}>
                      <span className="badge-roll">{item.rollNumber}</span>
                    </td>
                    <td style={{ padding: '12px 18px', color: '#94a3b8' }}>
                      {item.department}
                    </td>
                    <td style={{ padding: '12px 18px', color: '#94a3b8' }}>
                      {new Date(item.interviewDate!).toLocaleDateString()}
                    </td>
                    <td style={{ padding: '12px 18px', textAlign: 'center' }}>
                      {item.readinessScore != null ? (
                        <span style={{ fontWeight: 700, color: '#34d399' }}>
                          {item.readinessScore}%
                        </span>
                      ) : (
                        <span style={{ color: '#64748b' }}>—</span>
                      )}
                    </td>
                    <td style={{ padding: '12px 18px', textAlign: 'center' }}>
                      {item.overallScore != null ? (
                        <span style={{ fontWeight: 600, color: '#38bdf8' }}>
                          {item.overallScore}
                        </span>
                      ) : (
                        <span style={{ color: '#64748b' }}>—</span>
                      )}
                    </td>
                    <td style={{ padding: '12px 18px', textAlign: 'right' }}>
                      <button
                        onClick={() => onSelectStudent(item.rollNumber)}
                        className="btn-secondary"
                        style={{ padding: '5px 10px', fontSize: '0.75rem' }}
                      >
                        Review Feedback
                      </button>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
};
