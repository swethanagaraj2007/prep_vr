'use client';

import React, { useState, useMemo } from 'react';
import { StudentRanking } from '../lib/api';
import { Search, Filter, ArrowUpDown, ChevronRight, UserPlus, FileText, CheckCircle2, Clock, AlertCircle, RefreshCw } from 'lucide-react';

interface StudentRankingTableProps {
  students: StudentRanking[];
  isLoading: boolean;
  onSelectStudent: (studentIdOrRoll: string) => void;
  onOpenRegisterModal: () => void;
  onRefresh: () => void;
}

export const StudentRankingTable: React.FC<StudentRankingTableProps> = ({
  students,
  isLoading,
  onSelectStudent,
  onOpenRegisterModal,
  onRefresh,
}) => {
  const [searchTerm, setSearchTerm] = useState('');
  const [departmentFilter, setDepartmentFilter] = useState('All');
  const [statusFilter, setStatusFilter] = useState('All');
  const [sortBy, setSortBy] = useState<'rank' | 'readiness' | 'score' | 'date'>('rank');
  const [sortOrder, setSortOrder] = useState<'asc' | 'desc'>('asc');

  // Extract distinct departments for dropdown
  const departments = useMemo(() => {
    const set = new Set<string>();
    students.forEach((s) => {
      if (s.department) set.add(s.department);
    });
    return ['All', ...Array.from(set)];
  }, [students]);

  // Filtered and sorted students
  const filteredStudents = useMemo(() => {
    return students
      .filter((s) => {
        // Search filter
        if (searchTerm.trim() !== '') {
          const q = searchTerm.toLowerCase();
          const matchName = s.studentName.toLowerCase().includes(q);
          const matchRoll = s.rollNumber.toLowerCase().includes(q);
          const matchDept = s.department.toLowerCase().includes(q);
          if (!matchName && !matchRoll && !matchDept) return false;
        }

        // Department filter
        if (departmentFilter !== 'All' && s.department !== departmentFilter) {
          return false;
        }

        // Status filter
        if (statusFilter !== 'All' && s.interviewStatus !== statusFilter) {
          return false;
        }

        return true;
      })
      .sort((a, b) => {
        let comp = 0;
        if (sortBy === 'rank') {
          // Rank 0 (unranked) goes to bottom
          const rA = a.rank > 0 ? a.rank : 99999;
          const rB = b.rank > 0 ? b.rank : 99999;
          comp = rA - rB;
        } else if (sortBy === 'readiness') {
          comp = (b.readinessScore ?? -1) - (a.readinessScore ?? -1);
        } else if (sortBy === 'score') {
          comp = (b.overallScore ?? -1) - (a.overallScore ?? -1);
        } else if (sortBy === 'date') {
          const dA = a.interviewDate ? new Date(a.interviewDate).getTime() : 0;
          const dB = b.interviewDate ? new Date(b.interviewDate).getTime() : 0;
          comp = dB - dA;
        }

        return sortOrder === 'asc' ? comp : -comp;
      });
  }, [students, searchTerm, departmentFilter, statusFilter, sortBy, sortOrder]);

  const renderStatusBadge = (status: string) => {
    switch (status) {
      case 'COMPLETED':
        return (
          <span className="badge badge-completed">
            <CheckCircle2 size={12} />
            Completed
          </span>
        );
      case 'IN_PROGRESS':
        return (
          <span className="badge badge-in-progress">
            <div className="pulse-indicator" style={{ background: '#0ea5e9', width: '6px', height: '6px' }} />
            In Progress
          </span>
        );
      case 'FAILED':
        return (
          <span className="badge badge-failed">
            <AlertCircle size={12} />
            Failed
          </span>
        );
      case 'PENDING':
      default:
        return (
          <span className="badge badge-pending">
            <Clock size={12} />
            Pending
          </span>
        );
    }
  };

  return (
    <section className="glass-panel" style={{ margin: '0 24px 32px', padding: '24px' }}>
      {/* Table Header & Controls */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '16px', marginBottom: '20px' }}>
        <div>
          <h2 className="brand-font" style={{ fontSize: '1.35rem', fontWeight: 700, color: '#f8fafc' }}>
            Student Placement Directory & Rankings
          </h2>
          <p style={{ fontSize: '0.85rem', color: '#94a3b8', marginTop: '2px' }}>
            Monitor real-time VR interview attempts, readiness ratings, and institutional scores
          </p>
        </div>

        <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
          <a
            href="http://localhost:5000/api/reports/export-csv?type=rankings"
            target="_blank"
            rel="noreferrer"
            className="btn-secondary"
            style={{ textDecoration: 'none' }}
          >
            <FileText size={16} color="#38bdf8" />
            Export Rankings CSV
          </a>
          <button onClick={onRefresh} className="btn-secondary" title="Refresh Table">
            <RefreshCw size={16} className={isLoading ? 'spin' : ''} />
            Refresh
          </button>
          <button onClick={onOpenRegisterModal} className="btn-primary">
            <UserPlus size={16} />
            Register Student
          </button>
        </div>
      </div>

      {/* Official Ranking Policy & Rubric Card */}
      <div
        style={{
          marginBottom: '18px',
          padding: '12px 18px',
          borderRadius: 'var(--radius-md)',
          background: 'rgba(2, 132, 199, 0.08)',
          border: '1px solid rgba(56, 189, 248, 0.25)',
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          flexWrap: 'wrap',
          gap: '12px',
          fontSize: '0.8rem',
        }}
      >
        <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
          <CheckCircle2 size={16} color="#38bdf8" />
          <span style={{ color: '#e2e8f0' }}>
            <strong>Authoritative Placement Ranking Policy:</strong> Students ranked strictly by evaluated <strong>Readiness Score (out of 100)</strong>, computed across Technical Architecture (40%), Behavioral/STAR (30%), Communication (10%), Confidence (10%), and Professionalism (10%). Interrupted/Practice sessions are unranked.
          </span>
        </div>
        <span style={{ color: '#94a3b8', fontSize: '0.75rem' }}>
          Ties broken by recency & register order
        </span>
      </div>

      {/* Filter & Search Bar */}
      <div style={{ display: 'flex', flexWrap: 'wrap', gap: '12px', alignItems: 'center', padding: '14px 18px', background: 'rgba(12, 19, 34, 0.6)', borderRadius: 'var(--radius-md)', border: '1px solid var(--border-color)', marginBottom: '20px' }}>
        {/* Search input */}
        <div style={{ flex: '1 1 240px', position: 'relative' }}>
          <Search size={16} color="#64748b" style={{ position: 'absolute', left: '12px', top: '50%', transform: 'translateY(-50%)' }} />
          <input
            type="text"
            placeholder="Search by Roll Number, Name, Department..."
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            style={{ width: '100%', padding: '9px 12px 9px 36px', background: 'rgba(255, 255, 255, 0.05)', border: '1px solid var(--border-color)', borderRadius: '8px', color: '#f8fafc', fontSize: '0.875rem', outline: 'none' }}
          />
        </div>

        {/* Department Filter */}
        <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
          <Filter size={15} color="#94a3b8" />
          <span style={{ fontSize: '0.8rem', color: '#94a3b8' }}>Dept:</span>
          <select
            value={departmentFilter}
            onChange={(e) => setDepartmentFilter(e.target.value)}
            style={{ background: 'rgba(255, 255, 255, 0.06)', border: '1px solid var(--border-color)', borderRadius: '8px', padding: '8px 12px', color: '#f8fafc', fontSize: '0.825rem', outline: 'none', cursor: 'pointer' }}
          >
            {departments.map((d) => (
              <option key={d} value={d} style={{ background: '#0f172a', color: '#f8fafc' }}>
                {d === 'All' ? 'All Departments' : d}
              </option>
            ))}
          </select>
        </div>

        {/* Status Filter */}
        <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
          <span style={{ fontSize: '0.8rem', color: '#94a3b8' }}>Status:</span>
          <select
            value={statusFilter}
            onChange={(e) => setStatusFilter(e.target.value)}
            style={{ background: 'rgba(255, 255, 255, 0.06)', border: '1px solid var(--border-color)', borderRadius: '8px', padding: '8px 12px', color: '#f8fafc', fontSize: '0.825rem', outline: 'none', cursor: 'pointer' }}
          >
            <option value="All" style={{ background: '#0f172a' }}>All Statuses</option>
            <option value="COMPLETED" style={{ background: '#0f172a' }}>Completed</option>
            <option value="PENDING" style={{ background: '#0f172a' }}>Pending</option>
            <option value="IN_PROGRESS" style={{ background: '#0f172a' }}>In Progress</option>
            <option value="FAILED" style={{ background: '#0f172a' }}>Failed/Interrupted</option>
          </select>
        </div>

        {/* Sort By */}
        <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
          <ArrowUpDown size={15} color="#94a3b8" />
          <span style={{ fontSize: '0.8rem', color: '#94a3b8' }}>Sort:</span>
          <select
            value={sortBy}
            onChange={(e) => setSortBy(e.target.value as any)}
            style={{ background: 'rgba(255, 255, 255, 0.06)', border: '1px solid var(--border-color)', borderRadius: '8px', padding: '8px 12px', color: '#f8fafc', fontSize: '0.825rem', outline: 'none', cursor: 'pointer' }}
          >
            <option value="rank" style={{ background: '#0f172a' }}>Rank Order</option>
            <option value="readiness" style={{ background: '#0f172a' }}>Readiness Score</option>
            <option value="score" style={{ background: '#0f172a' }}>Overall Score</option>
            <option value="date" style={{ background: '#0f172a' }}>Interview Date</option>
          </select>
        </div>
      </div>

      {/* Table Container */}
      <div style={{ overflowX: 'auto', borderRadius: 'var(--radius-md)', border: '1px solid var(--border-color)' }}>
        <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '0.875rem' }}>
          <thead>
            <tr style={{ background: 'rgba(15, 23, 42, 0.95)', borderBottom: '1px solid var(--border-color)' }}>
              <th style={{ padding: '14px 16px', fontWeight: 600, color: '#94a3b8', width: '80px' }}>Rank</th>
              <th style={{ padding: '14px 16px', fontWeight: 600, color: '#94a3b8' }}>Roll Number</th>
              <th style={{ padding: '14px 16px', fontWeight: 600, color: '#94a3b8' }}>Student Name</th>
              <th style={{ padding: '14px 16px', fontWeight: 600, color: '#94a3b8' }}>Department</th>
              <th style={{ padding: '14px 16px', fontWeight: 600, color: '#94a3b8' }}>Interview Status</th>
              <th style={{ padding: '14px 16px', fontWeight: 600, color: '#94a3b8' }}>Overall Score</th>
              <th style={{ padding: '14px 16px', fontWeight: 600, color: '#94a3b8' }}>Readiness</th>
              <th style={{ padding: '14px 16px', fontWeight: 600, color: '#94a3b8' }}>Interview Date</th>
              <th style={{ padding: '14px 16px', fontWeight: 600, color: '#94a3b8', textAlign: 'center' }}>Action</th>
            </tr>
          </thead>
          <tbody>
            {students.length === 0 ? (
              <tr>
                <td colSpan={9} style={{ padding: '60px 20px', textAlign: 'center' }}>
                  <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: '8px' }}>
                    <div style={{ width: '48px', height: '48px', borderRadius: '50%', background: 'rgba(56, 189, 248, 0.1)', display: 'flex', alignItems: 'center', justifyContent: 'center', marginBottom: '8px' }}>
                      <CheckCircle2 size={24} color="#38bdf8" />
                    </div>
                    <div style={{ fontSize: '1rem', fontWeight: 600, color: '#f8fafc' }}>
                      No Unity VR Mock Interview Sessions Recorded Yet
                    </div>
                    <div style={{ fontSize: '0.85rem', color: '#94a3b8', maxWidth: '500px' }}>
                      Candidate rankings and evaluations will appear here automatically when students complete their mock interview sessions in the Unity VR application.
                    </div>
                  </div>
                </td>
              </tr>
            ) : filteredStudents.length === 0 ? (
              <tr>
                <td colSpan={9} style={{ padding: '40px', textAlign: 'center', color: '#64748b' }}>
                  No students found matching your criteria.
                </td>
              </tr>
            ) : (
              filteredStudents.map((s, idx) => (
                <tr
                  key={s.studentId}
                  style={{
                    borderBottom: '1px solid rgba(255, 255, 255, 0.04)',
                    background: idx % 2 === 0 ? 'rgba(24, 34, 52, 0.35)' : 'transparent',
                    transition: 'background 0.15s ease',
                  }}
                  onMouseEnter={(e) => (e.currentTarget.style.background = 'rgba(56, 189, 248, 0.08)')}
                  onMouseLeave={(e) =>
                    (e.currentTarget.style.background = idx % 2 === 0 ? 'rgba(24, 34, 52, 0.35)' : 'transparent')
                  }
                >
                  {/* Rank */}
                  <td style={{ padding: '14px 16px' }}>
                    {s.rank > 0 ? (
                      <span
                        style={{
                          display: 'inline-flex',
                          alignItems: 'center',
                          justifyContent: 'center',
                          width: '28px',
                          height: '28px',
                          borderRadius: '8px',
                          fontWeight: 700,
                          fontSize: '0.85rem',
                          background:
                            s.rank === 1
                              ? 'linear-gradient(135deg, #f59e0b, #d97706)'
                              : s.rank === 2
                              ? 'linear-gradient(135deg, #94a3b8, #64748b)'
                              : s.rank === 3
                              ? 'linear-gradient(135deg, #b45309, #78350f)'
                              : 'rgba(255, 255, 255, 0.08)',
                          color: '#ffffff',
                          boxShadow: s.rank <= 3 ? '0 2px 8px rgba(0,0,0,0.3)' : 'none',
                        }}
                      >
                        {s.rank}
                      </span>
                    ) : (
                      <span style={{ color: '#64748b', fontSize: '0.85rem' }}>—</span>
                    )}
                  </td>

                  {/* Roll Number */}
                  <td style={{ padding: '14px 16px' }}>
                    <span className="badge-roll">{s.rollNumber}</span>
                  </td>

                  {/* Name */}
                  <td style={{ padding: '14px 16px', fontWeight: 600, color: '#f8fafc' }}>
                    {s.studentName}
                  </td>

                  {/* Department */}
                  <td style={{ padding: '14px 16px', color: '#94a3b8', fontSize: '0.825rem' }}>
                    {s.department} <span style={{ opacity: 0.6 }}>(Yr {s.year})</span>
                  </td>

                  {/* Status */}
                  <td style={{ padding: '14px 16px' }}>{renderStatusBadge(s.interviewStatus)}</td>

                  {/* Overall Score */}
                  <td style={{ padding: '14px 16px' }}>
                    {s.overallScore != null ? (
                      <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                        <div
                          style={{
                            width: '48px',
                            height: '6px',
                            background: 'rgba(255, 255, 255, 0.1)',
                            borderRadius: '3px',
                            overflow: 'hidden',
                          }}
                        >
                          <div
                            style={{
                              width: `${Math.min(100, Math.max(0, s.overallScore))}%`,
                              height: '100%',
                              background: s.overallScore >= 85 ? '#10b981' : s.overallScore >= 70 ? '#38bdf8' : '#f59e0b',
                            }}
                          />
                        </div>
                        <span style={{ fontWeight: 600, color: '#f1f5f9' }}>{s.overallScore}</span>
                      </div>
                    ) : (
                      <span style={{ color: '#64748b' }}>—</span>
                    )}
                  </td>

                  {/* Readiness */}
                  <td style={{ padding: '14px 16px' }}>
                    {s.readinessScore != null ? (
                      <span
                        style={{
                          fontWeight: 700,
                          fontSize: '0.875rem',
                          color: s.readinessScore >= 90 ? '#34d399' : s.readinessScore >= 75 ? '#38bdf8' : '#fbbf24',
                        }}
                      >
                        {s.readinessScore}%
                      </span>
                    ) : (
                      <span style={{ color: '#64748b' }}>—</span>
                    )}
                  </td>

                  {/* Date */}
                  <td style={{ padding: '14px 16px', color: '#94a3b8', fontSize: '0.825rem' }}>
                    {s.interviewDate ? new Date(s.interviewDate).toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' }) : 'Pending'}
                  </td>

                  {/* Action */}
                  <td style={{ padding: '14px 16px', textAlign: 'center' }}>
                    <button
                      onClick={() => onSelectStudent(s.rollNumber)}
                      className="btn-secondary"
                      style={{ padding: '6px 12px', fontSize: '0.8rem', borderRadius: '6px' }}
                    >
                      <span>View Analysis</span>
                      <ChevronRight size={14} />
                    </button>
                  </td>
                </tr>
              ))
            )}
          </tbody>
        </table>
      </div>
    </section>
  );
};
