'use client';

import React, { useState, useEffect } from 'react';
import { getStudents, deleteStudent, DetailedStudent } from '../lib/api';
import {
  Search,
  Filter,
  UserPlus,
  UploadCloud,
  Edit2,
  Trash2,
  Eye,
  CheckCircle2,
  Clock,
  PlayCircle,
  XCircle,
  AlertCircle,
  GraduationCap
} from 'lucide-react';

interface StudentDirectoryViewProps {
  onSelectStudent: (studentIdOrRoll: string) => void;
  onOpenRegisterModal: () => void;
  onOpenImportCsvModal: () => void;
  onOpenEditModal: (student: DetailedStudent) => void;
  refreshTrigger: number;
}

export const StudentDirectoryView: React.FC<StudentDirectoryViewProps> = ({
  onSelectStudent,
  onOpenRegisterModal,
  onOpenImportCsvModal,
  onOpenEditModal,
  refreshTrigger,
}) => {
  const [students, setStudents] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [search, setSearch] = useState('');
  const [department, setDepartment] = useState('All');
  const [status, setStatus] = useState('All');
  const [deleteConfirm, setDeleteConfirm] = useState<string | null>(null);

  const fetchStudentList = async () => {
    try {
      setLoading(true);
      const data = await getStudents({
        search,
        department,
        status,
        sortBy: 'rollNumber',
        sortOrder: 'asc',
      });
      setStudents(data);
    } catch (err) {
      console.error('Failed to load students:', err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchStudentList();
  }, [search, department, status, refreshTrigger]);

  const handleDelete = async (id: string, roll: string) => {
    try {
      await deleteStudent(id);
      setDeleteConfirm(null);
      fetchStudentList();
    } catch (err: any) {
      alert(`Delete failed: ${err.message}`);
    }
  };

  const getStatusBadge = (interview: any) => {
    if (!interview || interview.interviewStatus === 'PENDING') {
      return (
        <span className="badge badge-pending">
          <Clock size={12} /> Pending
        </span>
      );
    }
    if (interview.interviewStatus === 'IN_PROGRESS') {
      return (
        <span className="badge badge-in-progress">
          <PlayCircle size={12} /> In Progress
        </span>
      );
    }
    if (interview.interviewStatus === 'FAILED') {
      return (
        <span className="badge badge-failed">
          <XCircle size={12} /> Interrupted
        </span>
      );
    }
    return (
      <span className="badge badge-completed">
        <CheckCircle2 size={12} /> Completed
      </span>
    );
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
        {/* Left: Search & Filters */}
        <div style={{ display: 'flex', alignItems: 'center', gap: '12px', flexWrap: 'wrap', flex: 1 }}>
          <div style={{ position: 'relative', minWidth: '240px' }}>
            <Search
              size={16}
              color="#64748b"
              style={{ position: 'absolute', left: '12px', top: '50%', transform: 'translateY(-50%)' }}
            />
            <input
              type="text"
              placeholder="Search by name, roll, email..."
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
            <option value="All">All Statuses</option>
            <option value="COMPLETED">Completed</option>
            <option value="IN_PROGRESS">In Progress</option>
            <option value="PENDING">Pending</option>
            <option value="FAILED">Interrupted</option>
          </select>
        </div>

        {/* Right: Actions */}
        <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
          <button
            onClick={onOpenImportCsvModal}
            className="btn-secondary"
            style={{ fontSize: '0.85rem' }}
          >
            <UploadCloud size={16} color="#38bdf8" />
            Import CSV
          </button>
          <button
            onClick={onOpenRegisterModal}
            className="btn-primary"
            style={{ fontSize: '0.85rem' }}
          >
            <UserPlus size={16} />
            Add Student
          </button>
        </div>
      </div>

      {/* Student Directory Table */}
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
            <GraduationCap size={20} color="#38bdf8" />
            <h2 style={{ fontSize: '1.05rem', fontWeight: 600, color: '#f8fafc' }}>
              Student Records & Placement Registry
            </h2>
          </div>
          <span style={{ fontSize: '0.8rem', color: '#94a3b8' }}>
            Showing <strong>{students.length}</strong> registered students
          </span>
        </div>

        <div style={{ overflowX: 'auto' }}>
          <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '0.85rem' }}>
            <thead>
              <tr style={{ borderBottom: '1px solid var(--border-color)', background: 'rgba(15, 23, 42, 0.4)', color: '#94a3b8' }}>
                <th style={{ padding: '12px 18px', fontWeight: 600 }}>Roll Number</th>
                <th style={{ padding: '12px 18px', fontWeight: 600 }}>Student Name</th>
                <th style={{ padding: '12px 18px', fontWeight: 600 }}>Department</th>
                <th style={{ padding: '12px 18px', fontWeight: 600 }}>Year</th>
                <th style={{ padding: '12px 18px', fontWeight: 600 }}>Email</th>
                <th style={{ padding: '12px 18px', fontWeight: 600 }}>Interview Status</th>
                <th style={{ padding: '12px 18px', fontWeight: 600, textAlign: 'center' }}>Readiness</th>
                <th style={{ padding: '12px 18px', fontWeight: 600, textAlign: 'center' }}>Score</th>
                <th style={{ padding: '12px 18px', fontWeight: 600, textAlign: 'right' }}>Actions</th>
              </tr>
            </thead>
            <tbody>
              {loading ? (
                <tr>
                  <td colSpan={9} style={{ padding: '36px', textAlign: 'center', color: '#94a3b8' }}>
                    Loading student cohort...
                  </td>
                </tr>
              ) : students.length === 0 ? (
                <tr>
                  <td colSpan={9} style={{ padding: '60px 20px', textAlign: 'center' }}>
                    <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: '8px' }}>
                      <div style={{ width: '48px', height: '48px', borderRadius: '50%', background: 'rgba(56, 189, 248, 0.1)', display: 'flex', alignItems: 'center', justifyContent: 'center', marginBottom: '8px' }}>
                        <GraduationCap size={24} color="#38bdf8" />
                      </div>
                      <div style={{ fontSize: '1rem', fontWeight: 600, color: '#f8fafc' }}>
                        No Student Records in Placement Database
                      </div>
                      <div style={{ fontSize: '0.85rem', color: '#94a3b8', maxWidth: '520px' }}>
                        Students can be pre-registered using &quot;Add Student&quot; or &quot;Import CSV&quot;, or will be auto-provisioned as candidates complete interviews in Unity VR.
                      </div>
                    </div>
                  </td>
                </tr>
              ) : (
                students.map((st) => {
                  const latest = st.latestInterview;
                  return (
                    <tr
                      key={st.id}
                      style={{
                        borderBottom: '1px solid rgba(255, 255, 255, 0.04)',
                        transition: 'background 0.15s ease',
                      }}
                      onMouseEnter={(e) => (e.currentTarget.style.background = 'rgba(255, 255, 255, 0.03)')}
                      onMouseLeave={(e) => (e.currentTarget.style.background = 'transparent')}
                    >
                      <td style={{ padding: '12px 18px' }}>
                        <span className="badge-roll">{st.rollNumber}</span>
                      </td>
                      <td style={{ padding: '12px 18px', fontWeight: 600, color: '#f1f5f9' }}>
                        {st.name}
                      </td>
                      <td style={{ padding: '12px 18px', color: '#94a3b8' }}>
                        {st.department}
                      </td>
                      <td style={{ padding: '12px 18px', color: '#94a3b8' }}>
                        Yr {st.year}
                      </td>
                      <td style={{ padding: '12px 18px', color: '#64748b', fontSize: '0.8rem' }}>
                        {st.email}
                      </td>
                      <td style={{ padding: '12px 18px' }}>
                        {getStatusBadge(latest)}
                      </td>
                      <td style={{ padding: '12px 18px', textAlign: 'center' }}>
                        {latest?.readinessScore != null ? (
                          <span style={{ fontWeight: 700, color: '#34d399' }}>
                            {latest.readinessScore}%
                          </span>
                        ) : (
                          <span style={{ color: '#64748b' }}>—</span>
                        )}
                      </td>
                      <td style={{ padding: '12px 18px', textAlign: 'center' }}>
                        {latest?.overallScore != null ? (
                          <span style={{ fontWeight: 600, color: '#38bdf8' }}>
                            {latest.overallScore}
                          </span>
                        ) : (
                          <span style={{ color: '#64748b' }}>—</span>
                        )}
                      </td>
                      <td style={{ padding: '12px 18px', textAlign: 'right' }}>
                        <div style={{ display: 'inline-flex', gap: '6px' }}>
                          <button
                            onClick={() => onSelectStudent(st.rollNumber)}
                            className="btn-secondary"
                            style={{ padding: '5px 8px', fontSize: '0.75rem' }}
                            title="View Profile & Feedback"
                          >
                            <Eye size={14} color="#38bdf8" />
                          </button>
                          <button
                            onClick={() => onOpenEditModal(st)}
                            className="btn-secondary"
                            style={{ padding: '5px 8px', fontSize: '0.75rem' }}
                            title="Edit Student"
                          >
                            <Edit2 size={14} color="#fbbf24" />
                          </button>
                          {deleteConfirm === st.id ? (
                            <button
                              onClick={() => handleDelete(st.id, st.rollNumber)}
                              style={{
                                padding: '5px 8px',
                                fontSize: '0.75rem',
                                background: '#dc2626',
                                border: '1px solid #ef4444',
                                borderRadius: '6px',
                                color: '#fff',
                                cursor: 'pointer',
                              }}
                              title="Confirm Delete"
                            >
                              Confirm
                            </button>
                          ) : (
                            <button
                              onClick={() => setDeleteConfirm(st.id)}
                              className="btn-secondary"
                              style={{ padding: '5px 8px', fontSize: '0.75rem' }}
                              title="Delete Student"
                            >
                              <Trash2 size={14} color="#f87171" />
                            </button>
                          )}
                        </div>
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
