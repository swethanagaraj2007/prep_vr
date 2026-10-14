'use client';

import React, { useState } from 'react';
import { importCsvStudents } from '../lib/api';
import { X, UploadCloud, FileText, CheckCircle2, AlertTriangle, AlertCircle } from 'lucide-react';

interface ImportCsvModalProps {
  isOpen: boolean;
  onClose: () => void;
  onSuccess: () => void;
}

export const ImportCsvModal: React.FC<ImportCsvModalProps> = ({
  isOpen,
  onClose,
  onSuccess,
}) => {
  const [csvContent, setCsvContent] = useState('');
  const [loading, setLoading] = useState(false);
  const [result, setResult] = useState<{
    totalProcessed: number;
    importedCount: number;
    skippedDuplicates: number;
    duplicateRollNumbers: string[];
    errors: string[];
  } | null>(null);
  const [error, setError] = useState<string | null>(null);

  if (!isOpen) return null;

  const handleFileUpload = (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    const reader = new FileReader();
    reader.onload = (event) => {
      const text = event.target?.result as string;
      setCsvContent(text);
      setResult(null);
      setError(null);
    };
    reader.readAsText(file);
  };

  const handleImport = async () => {
    if (!csvContent.trim()) {
      setError('Please provide CSV data or upload a file.');
      return;
    }

    try {
      setLoading(true);
      setError(null);
      const res = await importCsvStudents({ csvData: csvContent });
      setResult(res);
      onSuccess();
    } catch (err: any) {
      setError(err.message || 'Failed to import CSV records.');
    } finally {
      setLoading(false);
    }
  };

  const sampleCsv = `Roll Number,Name,Department,Year,Email
REC010,Siddharth V,Computer Science & Engineering,4,siddharth.rec010@college.edu
REC011,Meera Krishnan,Information Technology,4,meera.rec011@college.edu
REC012,Gautam Shenoy,AI & Data Science,4,gautam.rec012@college.edu`;

  return (
    <div
      style={{
        position: 'fixed',
        inset: 0,
        zIndex: 9999,
        background: 'rgba(5, 8, 16, 0.85)',
        backdropFilter: 'blur(8px)',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        padding: '20px',
      }}
    >
      <div
        className="glass-panel"
        style={{
          width: '100%',
          maxWidth: '640px',
          background: '#0d1527',
          border: '1px solid rgba(56, 189, 248, 0.3)',
          boxShadow: '0 25px 50px -12px rgba(0, 0, 0, 0.7)',
          overflow: 'hidden',
        }}
      >
        {/* Header */}
        <div
          style={{
            padding: '20px 24px',
            borderBottom: '1px solid var(--border-color)',
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
            background: 'rgba(15, 23, 42, 0.6)',
          }}
        >
          <div style={{ display: 'flex', alignItems: 'center', gap: '12px' }}>
            <div
              style={{
                width: '36px',
                height: '36px',
                borderRadius: '10px',
                background: 'rgba(56, 189, 248, 0.15)',
                border: '1px solid rgba(56, 189, 248, 0.3)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
              }}
            >
              <UploadCloud size={20} color="#38bdf8" />
            </div>
            <div>
              <h2 style={{ fontSize: '1.15rem', fontWeight: 700, color: '#f8fafc' }}>
                Import Students from CSV
              </h2>
              <p style={{ fontSize: '0.8rem', color: '#94a3b8' }}>
                Batch import cohort with duplicate checking
              </p>
            </div>
          </div>
          <button
            onClick={onClose}
            className="btn-secondary"
            style={{ padding: '6px 8px', borderRadius: '8px' }}
          >
            <X size={18} />
          </button>
        </div>

        {/* Content */}
        <div style={{ padding: '24px' }}>
          {error && (
            <div
              style={{
                marginBottom: '16px',
                padding: '12px 16px',
                borderRadius: '8px',
                background: 'rgba(239, 68, 68, 0.12)',
                border: '1px solid rgba(239, 68, 68, 0.3)',
                display: 'flex',
                alignItems: 'center',
                gap: '10px',
                color: '#fca5a5',
                fontSize: '0.85rem',
              }}
            >
              <AlertCircle size={18} color="#ef4444" />
              <span>{error}</span>
            </div>
          )}

          {result && (
            <div
              style={{
                marginBottom: '16px',
                padding: '16px',
                borderRadius: '8px',
                background: 'rgba(16, 185, 129, 0.1)',
                border: '1px solid rgba(16, 185, 129, 0.3)',
              }}
            >
              <div style={{ display: 'flex', alignItems: 'center', gap: '8px', color: '#34d399', fontWeight: 600, fontSize: '0.95rem', marginBottom: '8px' }}>
                <CheckCircle2 size={18} />
                <span>Import Operation Completed</span>
              </div>
              <div style={{ fontSize: '0.85rem', color: '#cbd5e1', display: 'flex', gap: '16px' }}>
                <span>Total: <strong>{result.totalProcessed}</strong></span>
                <span style={{ color: '#34d399' }}>Imported: <strong>{result.importedCount}</strong></span>
                <span style={{ color: '#fbbf24' }}>Duplicates Skipped: <strong>{result.skippedDuplicates}</strong></span>
              </div>
              {result.duplicateRollNumbers.length > 0 && (
                <div style={{ marginTop: '8px', fontSize: '0.75rem', color: '#94a3b8' }}>
                  Skipped existing rolls: {result.duplicateRollNumbers.join(', ')}
                </div>
              )}
            </div>
          )}

          {/* File Picker or Text Area */}
          <div style={{ marginBottom: '16px' }}>
            <label style={{ display: 'block', fontSize: '0.8rem', fontWeight: 600, color: '#cbd5e1', marginBottom: '8px' }}>
              Upload .csv file or paste raw CSV text
            </label>
            <div
              style={{
                border: '2px dashed rgba(255, 255, 255, 0.15)',
                borderRadius: '10px',
                padding: '16px',
                textAlign: 'center',
                background: 'rgba(15, 23, 42, 0.4)',
                marginBottom: '12px',
              }}
            >
              <input
                type="file"
                accept=".csv,text/csv"
                onChange={handleFileUpload}
                style={{ display: 'none' }}
                id="csvFileInput"
              />
              <label
                htmlFor="csvFileInput"
                style={{
                  display: 'inline-flex',
                  alignItems: 'center',
                  gap: '8px',
                  padding: '8px 16px',
                  borderRadius: '6px',
                  background: 'rgba(56, 189, 248, 0.15)',
                  border: '1px solid rgba(56, 189, 248, 0.3)',
                  color: '#38bdf8',
                  fontSize: '0.85rem',
                  fontWeight: 600,
                  cursor: 'pointer',
                }}
              >
                <FileText size={16} /> Choose CSV File
              </label>
              <p style={{ fontSize: '0.75rem', color: '#64748b', marginTop: '6px' }}>
                CSV format: Roll Number, Name, Department, Year, Email
              </p>
            </div>

            <textarea
              rows={6}
              value={csvContent}
              onChange={(e) => setCsvContent(e.target.value)}
              placeholder="Or paste CSV rows here..."
              style={{
                width: '100%',
                padding: '12px',
                borderRadius: '8px',
                background: 'rgba(15, 23, 42, 0.8)',
                border: '1px solid rgba(255, 255, 255, 0.12)',
                color: '#f8fafc',
                fontSize: '0.82rem',
                fontFamily: 'monospace',
                resize: 'vertical',
              }}
            />
          </div>

          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <button
              type="button"
              onClick={() => setCsvContent(sampleCsv)}
              style={{
                background: 'none',
                border: 'none',
                color: '#38bdf8',
                fontSize: '0.75rem',
                cursor: 'pointer',
                textDecoration: 'underline',
              }}
            >
              Load Sample Template
            </button>

            <div style={{ display: 'flex', gap: '10px' }}>
              <button
                type="button"
                onClick={onClose}
                className="btn-secondary"
                disabled={loading}
              >
                Close
              </button>
              <button
                type="button"
                onClick={handleImport}
                className="btn-primary"
                disabled={loading || !csvContent.trim()}
              >
                {loading ? 'Processing...' : 'Start Import'}
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};
