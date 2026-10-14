'use client';

import React, { useEffect, useState } from 'react';
import { DetailedStudent, getStudentById } from '../lib/api';
import {
  X,
  Award,
  Calendar,
  Clock,
  Mail,
  BookOpen,
  GraduationCap,
  CheckCircle2,
  AlertTriangle,
  Lightbulb,
  FileText,
  Eye,
  Activity,
  UserCheck,
  Download,
  ExternalLink,
  Layers,
  History,
} from 'lucide-react';

interface StudentDetailsModalProps {
  studentIdOrRoll: string | null;
  onClose: () => void;
}

export const StudentDetailsModal: React.FC<StudentDetailsModalProps> = ({
  studentIdOrRoll,
  onClose,
}) => {
  const [student, setStudent] = useState<DetailedStudent | null>(null);
  const [selectedInterviewIndex, setSelectedInterviewIndex] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!studentIdOrRoll) return;

    let isMounted = true;
    setLoading(true);
    setError(null);

    getStudentById(studentIdOrRoll)
      .then((data) => {
        if (isMounted) {
          setStudent(data);
          setSelectedInterviewIndex(0);
        }
      })
      .catch((err) => {
        if (isMounted) setError(err.message);
      })
      .finally(() => {
        if (isMounted) setLoading(false);
      });

    return () => {
      isMounted = false;
    };
  }, [studentIdOrRoll]);

  if (!studentIdOrRoll) return null;

  const currentInterview = student?.interviews[selectedInterviewIndex] || null;

  return (
    <div
      style={{
        position: 'fixed',
        inset: 0,
        backgroundColor: 'rgba(5, 8, 15, 0.85)',
        backdropFilter: 'blur(8px)',
        zIndex: 1000,
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        padding: '24px',
      }}
      onClick={onClose}
    >
      <div
        className="glass-panel"
        style={{
          width: '100%',
          maxWidth: '920px',
          maxHeight: '90vh',
          overflowY: 'auto',
          padding: '32px',
          background: '#0d1526',
          border: '1px solid rgba(56, 189, 248, 0.25)',
          boxShadow: '0 25px 50px -12px rgba(0, 0, 0, 0.75)',
          position: 'relative',
        }}
        onClick={(e) => e.stopPropagation()}
      >
        {/* Close Button */}
        <button
          onClick={onClose}
          style={{
            position: 'absolute',
            top: '20px',
            right: '20px',
            background: 'rgba(255, 255, 255, 0.08)',
            border: 'none',
            borderRadius: '50%',
            width: '36px',
            height: '36px',
            color: '#94a3b8',
            cursor: 'pointer',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'center',
            transition: 'all 0.2s ease',
          }}
          onMouseEnter={(e) => (e.currentTarget.style.color = '#ffffff')}
          onMouseLeave={(e) => (e.currentTarget.style.color = '#94a3b8')}
        >
          <X size={20} />
        </button>

        {loading ? (
          <div style={{ padding: '60px', textAlign: 'center', color: '#94a3b8' }}>
            <Activity size={32} className="spin" style={{ margin: '0 auto 16px', color: '#38bdf8' }} />
            <p>Loading candidate placement dossier from PostgreSQL...</p>
          </div>
        ) : error || !student ? (
          <div style={{ padding: '60px', textAlign: 'center', color: '#f87171' }}>
            <AlertTriangle size={36} style={{ margin: '0 auto 16px' }} />
            <p>{error || 'Student not found'}</p>
          </div>
        ) : (
          <div>
            {/* Header: Student Info */}
            <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', flexWrap: 'wrap', gap: '16px', borderBottom: '1px solid var(--border-color)', paddingBottom: '20px', marginBottom: '24px' }}>
              <div>
                <div style={{ display: 'flex', alignItems: 'center', gap: '10px' }}>
                  <h2 className="brand-font" style={{ fontSize: '1.65rem', fontWeight: 700, color: '#f8fafc' }}>
                    {student.name}
                  </h2>
                  <span className="badge-roll" style={{ fontSize: '0.9rem', padding: '4px 10px' }}>
                    {student.rollNumber}
                  </span>
                  <span
                    className={`badge ${
                      currentInterview?.interviewStatus === 'COMPLETED'
                        ? 'badge-completed'
                        : currentInterview?.interviewStatus === 'IN_PROGRESS'
                        ? 'badge-in-progress'
                        : 'badge-pending'
                    }`}
                  >
                    {currentInterview?.interviewStatus || 'PENDING'}
                  </span>
                </div>

                <div style={{ display: 'flex', alignItems: 'center', flexWrap: 'wrap', gap: '16px', marginTop: '10px', fontSize: '0.85rem', color: '#94a3b8' }}>
                  <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                    <BookOpen size={15} color="#38bdf8" />
                    <span>{student.department}</span>
                  </div>
                  <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                    <GraduationCap size={15} color="#38bdf8" />
                    <span>Year {student.year}</span>
                  </div>
                  <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                    <Mail size={15} color="#38bdf8" />
                    <span>{student.email}</span>
                  </div>
                </div>
              </div>

              {/* PDF Report Button */}
              {currentInterview?.reportUrl && (
                <a
                  href={`http://localhost:5000${currentInterview.reportUrl}`}
                  target="_blank"
                  rel="noreferrer"
                  className="btn-primary"
                  style={{ textDecoration: 'none' }}
                >
                  <FileText size={16} />
                  <span>Open PDF Assessment</span>
                  <ExternalLink size={14} />
                </a>
              )}
            </div>

            {/* Multiple Attempts Selector (if student has more than 1 attempt) */}
            {student.interviews.length > 1 && (
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '20px', padding: '10px 14px', background: 'rgba(255,255,255,0.03)', borderRadius: '8px', border: '1px solid var(--border-color)' }}>
                <History size={16} color="#94a3b8" />
                <span style={{ fontSize: '0.8rem', color: '#94a3b8' }}>Select Interview Session:</span>
                <div style={{ display: 'flex', gap: '6px' }}>
                  {student.interviews.map((iv, idx) => (
                    <button
                      key={iv.id}
                      onClick={() => setSelectedInterviewIndex(idx)}
                      style={{
                        padding: '4px 10px',
                        fontSize: '0.75rem',
                        borderRadius: '6px',
                        cursor: 'pointer',
                        border: selectedInterviewIndex === idx ? '1px solid #38bdf8' : '1px solid rgba(255,255,255,0.1)',
                        background: selectedInterviewIndex === idx ? 'rgba(56, 189, 248, 0.2)' : 'rgba(255,255,255,0.05)',
                        color: selectedInterviewIndex === idx ? '#38bdf8' : '#94a3b8',
                        fontWeight: selectedInterviewIndex === idx ? 600 : 400,
                      }}
                    >
                      Attempt #{student.interviews.length - idx} ({new Date(iv.interviewDate).toLocaleDateString()})
                    </button>
                  ))}
                </div>
              </div>
            )}

            {!currentInterview ? (
              <div style={{ padding: '40px', textAlign: 'center', background: 'rgba(245, 158, 11, 0.05)', borderRadius: '12px', border: '1px solid rgba(245, 158, 11, 0.2)' }}>
                <Clock size={36} color="#f59e0b" style={{ margin: '0 auto 12px' }} />
                <h4 style={{ color: '#fbbf24', fontSize: '1.1rem', fontWeight: 600 }}>Interview Pending</h4>
                <p style={{ color: '#94a3b8', fontSize: '0.875rem', marginTop: '6px' }}>
                  This student is registered in the placement cohort but has not yet undertaken the PREP VR interview session.
                </p>
              </div>
            ) : (
              <div>
                {/* Readiness Tier Banner */}
                <div
                  style={{
                    padding: '16px 20px',
                    borderRadius: 'var(--radius-md)',
                    background:
                      (currentInterview.readinessScore ?? 0) >= 90
                        ? 'linear-gradient(135deg, rgba(16, 185, 129, 0.15), rgba(5, 150, 105, 0.05))'
                        : 'linear-gradient(135deg, rgba(2, 132, 199, 0.15), rgba(3, 105, 161, 0.05))',
                    border: '1px solid rgba(56, 189, 248, 0.25)',
                    display: 'flex',
                    justifyContent: 'space-between',
                    alignItems: 'center',
                    flexWrap: 'wrap',
                    gap: '16px',
                    marginBottom: '24px',
                  }}
                >
                  <div>
                    <span style={{ fontSize: '0.75rem', fontWeight: 700, color: '#38bdf8', textTransform: 'uppercase', letterSpacing: '0.05em' }}>
                      Overall Institutional Assessment
                    </span>
                    <h3 style={{ fontSize: '1.25rem', fontWeight: 700, color: '#f8fafc', marginTop: '2px' }}>
                      {currentInterview.readinessLevel || 'Evaluated Candidate'}
                    </h3>
                  </div>

                  <div style={{ display: 'flex', alignItems: 'center', gap: '24px' }}>
                    <div style={{ textAlign: 'right' }}>
                      <span style={{ fontSize: '0.75rem', color: '#94a3b8' }}>Overall Score</span>
                      <div style={{ fontSize: '1.5rem', fontWeight: 700, color: '#f8fafc' }}>
                        {currentInterview.overallScore ?? '—'} <span style={{ fontSize: '0.9rem', color: '#64748b' }}>/ 100</span>
                      </div>
                    </div>

                    <div style={{ textAlign: 'right', borderLeft: '1px solid rgba(255,255,255,0.1)', paddingLeft: '20px' }}>
                      <span style={{ fontSize: '0.75rem', color: '#94a3b8' }}>Readiness Gauge</span>
                      <div style={{ fontSize: '1.5rem', fontWeight: 700, color: '#34d399' }}>
                        {currentInterview.readinessScore != null ? `${currentInterview.readinessScore}%` : '—'}
                      </div>
                    </div>
                  </div>
                </div>

                {/* Competency Scores Breakdown (0 - 10) */}
                <div style={{ marginBottom: '24px' }}>
                  <h4 style={{ fontSize: '0.95rem', fontWeight: 600, color: '#f8fafc', marginBottom: '14px', display: 'flex', alignItems: 'center', gap: '8px' }}>
                    <Award size={18} color="#38bdf8" />
                    Core Placement Competency Ratings (Scale 1 – 10)
                  </h4>

                  <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(150px, 1fr))', gap: '12px' }}>
                    {[
                      { name: 'Communication', val: currentInterview.communicationScore, color: '#0ea5e9' },
                      { name: 'Confidence', val: currentInterview.confidenceScore, color: '#38bdf8' },
                      { name: 'Professionalism', val: currentInterview.professionalismScore, color: '#10b981' },
                      { name: 'Behavioral', val: currentInterview.behavioralScore, color: '#8b5cf6' },
                      { name: 'Hiring Manager', val: currentInterview.hiringManagerScore, color: '#f59e0b' },
                    ].map((comp) => (
                      <div key={comp.name} className="glass-card" style={{ padding: '14px', textAlign: 'center' }}>
                        <span style={{ fontSize: '0.75rem', color: '#94a3b8', fontWeight: 500 }}>{comp.name}</span>
                        <div style={{ fontSize: '1.5rem', fontWeight: 700, color: comp.val ? comp.color : '#64748b', margin: '4px 0' }}>
                          {comp.val != null ? `${comp.val}` : '—'}
                        </div>
                        <div style={{ height: '4px', background: 'rgba(255,255,255,0.08)', borderRadius: '2px', overflow: 'hidden' }}>
                          <div style={{ width: `${comp.val ? comp.val * 10 : 0}%`, height: '100%', background: comp.color }} />
                        </div>
                      </div>
                    ))}
                  </div>
                </div>

                {/* 3-Stage Breakdown (HR, Behavioral, Hiring Manager) */}
                {currentInterview.stages && currentInterview.stages.length > 0 && (
                  <div style={{ marginBottom: '24px' }}>
                    <h4 style={{ fontSize: '0.95rem', fontWeight: 600, color: '#f8fafc', marginBottom: '14px', display: 'flex', alignItems: 'center', gap: '8px' }}>
                      <Layers size={18} color="#38bdf8" />
                      10-Minute 3-Stage Panel Execution
                    </h4>

                    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))', gap: '12px' }}>
                      {currentInterview.stages.map((st) => (
                        <div key={st.id} className="glass-card" style={{ padding: '16px' }}>
                          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px' }}>
                            <span style={{ fontSize: '0.75rem', fontWeight: 700, color: '#38bdf8' }}>STAGE {st.stageNumber}</span>
                            <span style={{ fontSize: '0.75rem', color: '#94a3b8' }}>{Math.round(st.durationSeconds / 60)}m duration</span>
                          </div>
                          <h5 style={{ fontSize: '0.95rem', fontWeight: 600, color: '#f8fafc', marginBottom: '6px' }}>
                            {st.stageName}
                          </h5>
                          <p style={{ fontSize: '0.8rem', color: '#94a3b8', lineHeight: 1.4 }}>
                            {st.transcriptSummary || 'Evaluated candidate responses during this round.'}
                          </p>
                          <div style={{ marginTop: '10px', display: 'flex', gap: '12px', fontSize: '0.75rem', color: '#64748b' }}>
                            <span><b>{st.questionCount}</b> Questions</span>
                            <span><b>{st.answerCount}</b> Responses</span>
                          </div>
                        </div>
                      ))}
                    </div>
                  </div>
                )}

                {/* Qualitative Evidence-Based Feedback */}
                {currentInterview.feedback && (
                  <div style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
                    {/* Executive Summary */}
                    <div className="glass-card" style={{ padding: '18px' }}>
                      <h5 style={{ fontSize: '0.85rem', fontWeight: 700, color: '#38bdf8', textTransform: 'uppercase', marginBottom: '8px' }}>
                        Executive Interview Summary
                      </h5>
                      <p style={{ fontSize: '0.875rem', color: '#cbd5e1', lineHeight: 1.6 }}>
                        {currentInterview.feedback.overallSummary}
                      </p>
                    </div>

                    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(360px, 1fr))', gap: '16px' }}>
                      {/* Strengths */}
                      <div className="glass-card" style={{ padding: '18px', borderLeft: '3px solid #10b981' }}>
                        <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '12px' }}>
                          <CheckCircle2 size={18} color="#10b981" />
                          <h5 style={{ fontSize: '0.875rem', fontWeight: 700, color: '#34d399' }}>Verified Strengths</h5>
                        </div>
                        <ul style={{ listStyle: 'none', display: 'flex', flexDirection: 'column', gap: '8px' }}>
                          {currentInterview.feedback.strengths.map((str, i) => (
                            <li key={i} style={{ fontSize: '0.825rem', color: '#e2e8f0', lineHeight: 1.4, display: 'flex', gap: '8px' }}>
                              <span style={{ color: '#10b981' }}>✔</span>
                              <span>{str}</span>
                            </li>
                          ))}
                        </ul>
                      </div>

                      {/* Growth Areas */}
                      <div className="glass-card" style={{ padding: '18px', borderLeft: '3px solid #f59e0b' }}>
                        <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '12px' }}>
                          <AlertTriangle size={18} color="#f59e0b" />
                          <h5 style={{ fontSize: '0.875rem', fontWeight: 700, color: '#fbbf24' }}>Priority Areas for Growth</h5>
                        </div>
                        <ul style={{ listStyle: 'none', display: 'flex', flexDirection: 'column', gap: '8px' }}>
                          {currentInterview.feedback.weaknesses.map((wk, i) => (
                            <li key={i} style={{ fontSize: '0.825rem', color: '#e2e8f0', lineHeight: 1.4, display: 'flex', gap: '8px' }}>
                              <span style={{ color: '#f59e0b' }}>⚠</span>
                              <span>{wk}</span>
                            </li>
                          ))}
                        </ul>
                      </div>
                    </div>

                    {/* Actionable Recommendations */}
                    {currentInterview.feedback.recommendations && currentInterview.feedback.recommendations.length > 0 && (
                      <div className="glass-card" style={{ padding: '18px', borderLeft: '3px solid #0284c7' }}>
                        <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginBottom: '12px' }}>
                          <Lightbulb size={18} color="#38bdf8" />
                          <h5 style={{ fontSize: '0.875rem', fontWeight: 700, color: '#38bdf8' }}>Actionable Placement Preparation Roadmap</h5>
                        </div>
                        <div style={{ display: 'flex', flexDirection: 'column', gap: '8px' }}>
                          {currentInterview.feedback.recommendations.map((rec, i) => (
                            <div key={i} style={{ fontSize: '0.825rem', color: '#cbd5e1', lineHeight: 1.5, background: 'rgba(255,255,255,0.02)', padding: '8px 12px', borderRadius: '6px' }}>
                              {rec}
                            </div>
                          ))}
                        </div>
                      </div>
                    )}

                    {/* Demeanor */}
                    {(currentInterview.feedback.observableEyeContact || currentInterview.feedback.observableEngagement) && (
                      <div className="glass-card" style={{ padding: '16px' }}>
                        <h5 style={{ fontSize: '0.8rem', fontWeight: 700, color: '#94a3b8', textTransform: 'uppercase', marginBottom: '8px' }}>
                          Observable Demeanor & Cadence
                        </h5>
                        <p style={{ fontSize: '0.8rem', color: '#94a3b8', lineHeight: 1.5 }}>
                          {currentInterview.feedback.observableEyeContact} {currentInterview.feedback.observablePostureBodyLanguage} {currentInterview.feedback.observableEngagement}
                        </p>
                      </div>
                    )}
                  </div>
                )}
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  );
};
