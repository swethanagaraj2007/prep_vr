'use client';

import React from 'react';
import { DashboardSummary } from '../lib/api';
import { Users, CheckCircle2, Clock, Target, Award, Sparkles, TrendingUp, AlertTriangle } from 'lucide-react';

interface MetricsOverviewProps {
  summary: DashboardSummary | null;
  isLoading: boolean;
}

export const MetricsOverview: React.FC<MetricsOverviewProps> = ({ summary, isLoading }) => {
  if (isLoading || !summary) {
    return (
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '16px', margin: '0 24px 24px' }}>
        {[1, 2, 3, 4, 5].map((i) => (
          <div key={i} className="glass-card" style={{ padding: '20px', minHeight: '110px', opacity: 0.5 }}>
            <div style={{ width: '40%', height: '14px', background: 'rgba(255,255,255,0.1)', borderRadius: '4px', marginBottom: '12px' }} />
            <div style={{ width: '60%', height: '28px', background: 'rgba(255,255,255,0.15)', borderRadius: '6px' }} />
          </div>
        ))}
      </div>
    );
  }

  const completionRate = summary.totalStudents > 0
    ? Math.round((summary.completedInterviews / summary.totalStudents) * 100)
    : 0;

  return (
    <section style={{ margin: '0 24px 24px' }}>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '16px' }}>
        {/* Total Students */}
        <div className="glass-card" style={{ padding: '20px', position: 'relative', overflow: 'hidden' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
            <div>
              <p style={{ fontSize: '0.8rem', fontWeight: 600, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.05em' }}>
                Total Enrolled
              </p>
              <h3 className="brand-font" style={{ fontSize: '2rem', fontWeight: 700, marginTop: '4px', color: '#f8fafc' }}>
                {summary.totalStudents}
              </h3>
            </div>
            <div style={{ padding: '10px', borderRadius: '12px', background: 'rgba(56, 189, 248, 0.1)', color: '#38bdf8' }}>
              <Users size={22} />
            </div>
          </div>
          <div style={{ marginTop: '12px', display: 'flex', alignItems: 'center', gap: '6px', fontSize: '0.75rem', color: '#94a3b8' }}>
            <Sparkles size={14} color="#38bdf8" />
            <span>Eligible placement cohort</span>
          </div>
        </div>

        {/* Completed Interviews */}
        <div className="glass-card" style={{ padding: '20px', position: 'relative', overflow: 'hidden' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
            <div>
              <p style={{ fontSize: '0.8rem', fontWeight: 600, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.05em' }}>
                Completed VR Tests
              </p>
              <h3 className="brand-font" style={{ fontSize: '2rem', fontWeight: 700, marginTop: '4px', color: '#34d399' }}>
                {summary.completedInterviews}
              </h3>
            </div>
            <div style={{ padding: '10px', borderRadius: '12px', background: 'rgba(16, 185, 129, 0.1)', color: '#10b981' }}>
              <CheckCircle2 size={22} />
            </div>
          </div>
          <div style={{ marginTop: '12px', display: 'flex', alignItems: 'center', gap: '6px', fontSize: '0.75rem', color: '#34d399' }}>
            <TrendingUp size={14} />
            <span><b>{completionRate}%</b> cohort completion rate</span>
          </div>
        </div>

        {/* Pending & In Progress */}
        <div className="glass-card" style={{ padding: '20px', position: 'relative', overflow: 'hidden' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
            <div>
              <p style={{ fontSize: '0.8rem', fontWeight: 600, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.05em' }}>
                Pending / Active
              </p>
              <div style={{ display: 'flex', alignItems: 'baseline', gap: '8px', marginTop: '4px' }}>
                <h3 className="brand-font" style={{ fontSize: '2rem', fontWeight: 700, color: '#fbbf24' }}>
                  {summary.pendingInterviews}
                </h3>
                {summary.inProgressInterviews > 0 && (
                  <span style={{ fontSize: '0.8rem', color: '#38bdf8', fontWeight: 600 }}>
                    (+{summary.inProgressInterviews} In VR)
                  </span>
                )}
              </div>
            </div>
            <div style={{ padding: '10px', borderRadius: '12px', background: 'rgba(245, 158, 11, 0.1)', color: '#f59e0b' }}>
              <Clock size={22} />
            </div>
          </div>
          <div style={{ marginTop: '12px', display: 'flex', alignItems: 'center', gap: '6px', fontSize: '0.75rem', color: '#94a3b8' }}>
            <span>Requires VR headset allocation</span>
          </div>
        </div>

        {/* Average Readiness */}
        <div className="glass-card" style={{ padding: '20px', position: 'relative', overflow: 'hidden' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
            <div>
              <p style={{ fontSize: '0.8rem', fontWeight: 600, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.05em' }}>
                Average Readiness
              </p>
              <h3 className="brand-font" style={{ fontSize: '2rem', fontWeight: 700, marginTop: '4px', color: '#38bdf8' }}>
                {summary.averageReadiness > 0 ? `${summary.averageReadiness}%` : 'N/A'}
              </h3>
            </div>
            <div style={{ padding: '10px', borderRadius: '12px', background: 'rgba(2, 132, 199, 0.1)', color: '#0284c7' }}>
              <Target size={22} />
            </div>
          </div>
          <div style={{ marginTop: '12px', display: 'flex', alignItems: 'center', gap: '6px', fontSize: '0.75rem', color: '#38bdf8' }}>
            <span>Benchmark: <b>Proficient Tier</b></span>
          </div>
        </div>

        {/* Average & Top Score */}
        <div className="glass-card" style={{ padding: '20px', position: 'relative', overflow: 'hidden' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start' }}>
            <div>
              <p style={{ fontSize: '0.8rem', fontWeight: 600, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.05em' }}>
                Top Score / Avg Score
              </p>
              <div style={{ display: 'flex', alignItems: 'baseline', gap: '8px', marginTop: '4px' }}>
                <h3 className="brand-font" style={{ fontSize: '2rem', fontWeight: 700, color: '#f8fafc' }}>
                  {summary.highestScore > 0 ? Math.round(summary.highestScore) : '—'}
                </h3>
                <span style={{ fontSize: '0.9rem', color: '#94a3b8' }}>
                  / {summary.averageScore > 0 ? `${summary.averageScore} avg` : '—'}
                </span>
              </div>
            </div>
            <div style={{ padding: '10px', borderRadius: '12px', background: 'rgba(244, 63, 94, 0.1)', color: '#f43f5e' }}>
              <Award size={22} />
            </div>
          </div>
          <div style={{ marginTop: '12px', display: 'flex', alignItems: 'center', gap: '6px', fontSize: '0.75rem', color: '#94a3b8' }}>
            <span>Scored across 13 competencies</span>
          </div>
        </div>
      </div>
    </section>
  );
};
