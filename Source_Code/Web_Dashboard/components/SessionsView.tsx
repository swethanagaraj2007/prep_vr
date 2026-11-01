'use client';

import React, { useState, useEffect } from 'react';
import { getInterviews, DetailedInterview } from '../lib/api';
import {
  Video,
  CheckCircle2,
  Clock,
  PlayCircle,
  XCircle,
  AlertTriangle,
  Search,
  Filter,
  Eye,
  Calendar,
  Layers,
  ChevronRight
} from 'lucide-react';

interface SessionsViewProps {
  onSelectStudent: (rollOrId: string) => void;
  refreshTrigger: number;
}

export const SessionsView: React.FC<SessionsViewProps> = ({ onSelectStudent, refreshTrigger }) => {
  const [interviews, setInterviews] = useState<DetailedInterview[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(true);
  const [status, setStatus] = useState('All');
  const [department, setDepartment] = useState('All');
  const [search, setSearch] = useState('');

  const fetchSessions = async () => {
    try {
      setLoading(true);
      const res = await getInterviews({
        status,
        department,
        search,
        sortBy: 'interviewDate',
        sortOrder: 'desc',
      });
      setInterviews(res.interviews);
      setTotalCount(res.total);
    } catch (err) {
      console.error('Failed to load interview sessions:', err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchSessions();
  }, [status, department, search, refreshTrigger]);

  const getStatusBadge = (st: string) => {
    switch (st) {
      case 'COMPLETED':
        return (
          <span className="badge badge-completed">
            <CheckCircle2 size={12} /> Completed
          </span>
        );
      case 'IN_PROGRESS':
        return (
          <span className="badge badge-in-progress">
            <PlayCircle size={12} /> Live Simulation
          </span>
        );
      case 'FAILED':
        return (
          <span className="badge badge-failed">
            <XCircle size={12} /> Interrupted
          </span>
        );
      case 'PENDING':
      default:
        return (
          <span className="badge badge-pending">
            <Clock size={12} /> Pending VR Session
          </span>
        );
    }
  };

  return (
    <div style={{ padding: '0 24px 32px 24px' }}>
      {/* Control Bar */}
      <div
        className="glass-panel"
        style={{
          padding: '16px 20px',
          marginBottom: '20px',
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          flexWrap: 'wrap',
          gap: '16px',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: '12px', flexWrap: 'wrap', flex: 1 }}>
          <div style={{ position: 'relative', minWidth: '240px' }}>
            <Search
              size={16}
              color="#64748b"
              style={{ position: 'absolute', left: '12px', top: '50%', transform: 'translateY(-50%)' }}
            />
            <input
              type="text"
              placeholder="Search by candidate name or roll..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
              style={{
                width: '100%',
                padding: '8px 12px 8px 36px',
                borderRadius: '8px',
                background: 'rgba(15, 23, 42, 0.8)',
                border: '1px solid rgba(255, 255, 255, 0.1)',
                color: '#f8fafc',
                fontSize: '0.85rem',
              }}
            />
          </div>

          <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
            <Filter size={14} color="#94a3b8" />
            <select
              value={status}
              onChange={(e) => setStatus(e.target.value)}
              style={{
                padding: '8px 12px',
                borderRadius: '8px',
                background: 'rgba(15, 23, 42, 0.8)',
                border: '1px solid rgba(255, 255, 255, 0.1)',
                color: '#f8fafc',
                fontSize: '0.85rem',
              }}
            >
              <option value="All">All Session States</option>
              <option value="COMPLETED">Completed</option>
              <option value="IN_PROGRESS">In Progress (Live)</option>
              <option value="FAILED">Interrupted / Failed</option>
              <option value="PENDING">Pending Evaluation</option>
            </select>
          </div>

          <select
            value={department}
            onChange={(e) => setDepartment(e.target.value)}
            style={{
              padding: '8px 12px',
              borderRadius: '8px',
              background: 'rgba(15, 23, 42, 0.8)',
              border: '1px solid rgba(255, 255, 255, 0.1)',
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

        <div style={{ fontSize: '0.8rem', color: '#94a3b8' }}>
          Total Sessions Logged: <strong style={{ color: '#38bdf8' }}>{totalCount}</strong>
        </div>
      </div>

      {/* Sessions Table */}
      <div className="glass-panel" style={{ overflow: 'hidden' }}>
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
          <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
            <Video size={20} color="#38bdf8" />
            <h2 style={{ fontSize: '1.05rem', fontWeight: 600, color: '#f8fafc' }}>
              VR Interview Simulation Sessions Log
            </h2>
          </div>
          <span style={{ fontSize: '0.75rem', color: '#64748b' }}>
            Real-time synchronization with Unity headset client
          </span>
        </div>

        <div style={{ overflowX: 'auto' }}>
          <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '0.85rem' }}>
            <thead>
              <tr style={{ borderBottom: '1px solid var(--border-color)', background: 'rgba(15, 23, 42, 0.4)', color: '#94a3b8' }}>
                <th style={{ padding: '12px 18px', fontWeight: 600 }}>Session Timestamp</th>
                <th style={{ padding: '12px 18px', fontWeight: 600 }}>Candidate</th>
                <th style={{ padding: '12px 18px', fontWeight: 600 }}>Department</th>
                <th style={{ padding: '12px 18px', fontWeight: 600 }}>Status</th>
                <th style={{ padding: '12px 18px', fontWeight: 600 }}>Duration</th>
                <th style={{ padding: '12px 18px', fontWeight: 600, textAlign: 'center' }}>Stages Evaluated</th>
                <th style={{ padding: '12px 18px', fontWeight: 600, textAlign: 'center' }}>Readiness</th>
                <th style={{ padding: '12px 18px', fontWeight: 600, textAlign: 'center' }}>Overall Score</th>
                <th style={{ padding: '12px 18px', fontWeight: 600, textAlign: 'right' }}>Actions</th>
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr>
                  <td colSpan={9} style={{ padding: '36px', textAlign: 'center', color: '#94a3b8' }}>
                    Loading sessions log...
                  </td>
                </tr>
              ) : interviews.length === 0 ? (
                <tr>
                  <td colSpan={9} style={{ padding: '60px 20px', textAlign: 'center' }}>
                    <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: '8px' }}>
                      <div style={{ width: '48px', height: '48px', borderRadius: '50%', background: 'rgba(56, 189, 248, 0.1)', display: 'flex', alignItems: 'center', justifyContent: 'center', marginBottom: '8px' }}>
                        <Video size={24} color="#38bdf8" />
                      </div>
                      <div style={{ fontSize: '1rem', fontWeight: 600, color: '#f8fafc' }}>
                        No VR Mock Interview Sessions Logged Yet
                      </div>
                      <div style={{ fontSize: '0.85rem', color: '#94a3b8', maxWidth: '520px' }}>
                        As candidates launch and complete mock interview simulations in Unity VR, session timestamps, durations, and multi-stage evaluations will stream directly here.
                      </div>
                    </div>
                  </td>
                </tr>
              ) : (
                interviews.map((session) => {
                  const durationMins = Math.floor(session.interviewDuration / 60);
                  const durationSecs = session.interviewDuration % 60;
                  const durationText = `${durationMins}m ${durationSecs}s`;
                  const studentName = (session as any).student?.name || 'Candidate';
                  const studentDept = (session as any).student?.department || 'General';

                  return (
                    <tr
                      key={session.id}
                      style={{
                        borderBottom: '1px solid rgba(255, 255, 255, 0.04)',
                        transition: 'background 0.15s ease',
                      }}
                      onMouseEnter={(e) => (e.currentTarget.style.background = 'rgba(255, 255, 255, 0.03)')}
                      onMouseLeave={(e) => (e.currentTarget.style.background = 'transparent')}
                    >
                      <td style={{ padding: '12px 18px', color: '#cbd5e1' }}>
                        <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                          <Calendar size={13} color="#64748b" />
                          <span>{new Date(session.interviewDate).toLocaleString([], { dateStyle: 'short', timeStyle: 'short' })}</span>
                        </div>
                      </td>
                      <td style={{ padding: '12px 18px' }}>
                        <div style={{ fontWeight: 600, color: '#f1f5f9' }}>{studentName}</div>
                        <span className="badge-roll" style={{ fontSize: '0.72rem' }}>{session.rollNumber}</span>
                      </td>
                      <td style={{ padding: '12px 18px', color: '#94a3b8' }}>
                        {studentDept}
                      </td>
                      <td style={{ padding: '12px 18px' }}>
                        {getStatusBadge(session.interviewStatus)}
                      </td>
                      <td style={{ padding: '12px 18px', color: '#94a3b8' }}>
                        {durationText}
                      </td>
                      <td style={{ padding: '12px 18px', textAlign: 'center' }}>
                        <span
                          style={{
                            display: 'inline-flex',
                            alignItems: 'center',
                            gap: '4px',
                            padding: '3px 8px',
                            borderRadius: '6px',
                            background: 'rgba(255, 255, 255, 0.05)',
                            fontSize: '0.75rem',
                            color: '#94a3b8',
                          }}
                        >
                          <Layers size={12} color="#38bdf8" />
                          {session.stages?.length || 0} / 3 Stages
                        </span>
                      </td>
                      <td style={{ padding: '12px 18px', textAlign: 'center' }}>
                        {session.readinessScore != null ? (
                          <span style={{ fontWeight: 700, color: '#34d399' }}>
                            {session.readinessScore}%
                          </span>
                        ) : (
                          <span style={{ color: '#64748b' }}>—</span>
                        )}
                      </td>
                      <td style={{ padding: '12px 18px', textAlign: 'center' }}>
                        {session.overallScore != null ? (
                          <span style={{ fontWeight: 600, color: '#38bdf8' }}>
                            {session.overallScore}
                          </span>
                        ) : (
                          <span style={{ color: '#64748b' }}>—</span>
                        )}
                      </td>
                      <td style={{ padding: '12px 18px', textAlign: 'right' }}>
                        <button
                          onClick={() => onSelectStudent(session.rollNumber)}
                          className="btn-secondary"
                          style={{ padding: '5px 10px', fontSize: '0.75rem', display: 'inline-flex', alignItems: 'center', gap: '4px' }}
                        >
                          <Eye size={13} color="#38bdf8" /> Details
                        </button>
                      </td>
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
};
