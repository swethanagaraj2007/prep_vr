'use client';

import React, { useState } from 'react';
import { useAuth } from '../lib/auth-context';
import { Award, Lock, Mail, ArrowRight, ShieldCheck, AlertCircle } from 'lucide-react';

export const LoginPage: React.FC = () => {
  const { login } = useAuth();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null);
    setIsSubmitting(true);

    try {
      await login(email, password);
    } catch (err: any) {
      setError(err.message || 'Invalid placement credentials');
    } finally {
      setIsSubmitting(false);
    }
  };

  const autofillDemo = () => {
    setEmail('admin@prepvr.edu');
    setPassword('Placement@2026');
  };

  return (
    <div
      style={{
        minHeight: '100vh',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        padding: '24px',
        background: 'radial-gradient(ellipse at top, #111e38 0%, #080d1a 100%)',
      }}
    >
      <div
        className="glass-panel"
        style={{
          width: '100%',
          maxWidth: '460px',
          padding: '36px',
          background: '#0d1526',
          border: '1px solid rgba(56, 189, 248, 0.25)',
          boxShadow: '0 25px 50px -12px rgba(0, 0, 0, 0.7)',
        }}
      >
        {/* Brand Banner */}
        <div style={{ textAlign: 'center', marginBottom: '28px' }}>
          <div
            style={{
              width: '56px',
              height: '56px',
              borderRadius: '16px',
              background: 'linear-gradient(135deg, #0284c7 0%, #38bdf8 100%)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              margin: '0 auto 16px',
              boxShadow: '0 8px 24px rgba(2, 132, 199, 0.45)',
            }}
          >
            <Award size={32} color="#ffffff" />
          </div>
          <h2 className="brand-font" style={{ fontSize: '1.75rem', fontWeight: 800, color: '#f8fafc', letterSpacing: '-0.02em' }}>
            PREP VR
          </h2>
          <p style={{ fontSize: '0.875rem', color: '#94a3b8', marginTop: '4px' }}>
            Centralized Student Placement Data System
          </p>
          <div
            style={{
              display: 'inline-flex',
              alignItems: 'center',
              gap: '6px',
              marginTop: '10px',
              padding: '4px 12px',
              borderRadius: '20px',
              background: 'rgba(56, 189, 248, 0.1)',
              border: '1px solid rgba(56, 189, 248, 0.25)',
              fontSize: '0.75rem',
              color: '#38bdf8',
              fontWeight: 600,
            }}
          >
            <ShieldCheck size={14} />
            PLACEMENT FACULTY & STAFF PORTAL
          </div>
        </div>

        {error && (
          <div
            style={{
              padding: '12px 16px',
              borderRadius: '8px',
              background: 'rgba(239, 68, 68, 0.12)',
              border: '1px solid rgba(239, 68, 68, 0.3)',
              color: '#f87171',
              fontSize: '0.85rem',
              display: 'flex',
              alignItems: 'center',
              gap: '10px',
              marginBottom: '20px',
            }}
          >
            <AlertCircle size={18} />
            <span>{error}</span>
          </div>
        )}

        <form onSubmit={handleSubmit} style={{ display: 'flex', flexDirection: 'column', gap: '16px' }}>
          <div>
            <label style={{ display: 'block', fontSize: '0.825rem', fontWeight: 600, color: '#94a3b8', marginBottom: '6px' }}>
              Faculty / Placement Email
            </label>
            <div style={{ position: 'relative' }}>
              <Mail size={16} color="#64748b" style={{ position: 'absolute', left: '12px', top: '50%', transform: 'translateY(-50%)' }} />
              <input
                type="email"
                required
                placeholder="admin@prepvr.edu"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                style={{
                  width: '100%',
                  padding: '11px 12px 11px 38px',
                  background: 'rgba(255, 255, 255, 0.05)',
                  border: '1px solid var(--border-color)',
                  borderRadius: '8px',
                  color: '#f8fafc',
                  fontSize: '0.9rem',
                  outline: 'none',
                }}
              />
            </div>
          </div>

          <div>
            <label style={{ display: 'block', fontSize: '0.825rem', fontWeight: 600, color: '#94a3b8', marginBottom: '6px' }}>
              Password
            </label>
            <div style={{ position: 'relative' }}>
              <Lock size={16} color="#64748b" style={{ position: 'absolute', left: '12px', top: '50%', transform: 'translateY(-50%)' }} />
              <input
                type="password"
                required
                placeholder="••••••••"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                style={{
                  width: '100%',
                  padding: '11px 12px 11px 38px',
                  background: 'rgba(255, 255, 255, 0.05)',
                  border: '1px solid var(--border-color)',
                  borderRadius: '8px',
                  color: '#f8fafc',
                  fontSize: '0.9rem',
                  outline: 'none',
                }}
              />
            </div>
          </div>

          <button
            type="submit"
            disabled={isSubmitting}
            className="btn-primary"
            style={{
              width: '100%',
              justifyContent: 'center',
              padding: '12px',
              fontSize: '0.95rem',
              marginTop: '8px',
            }}
          >
            <span>{isSubmitting ? 'Authenticating...' : 'Sign In to Placement Dashboard'}</span>
            <ArrowRight size={16} />
          </button>
        </form>

        {/* Demo Credentials Helper */}
        <div
          style={{
            marginTop: '24px',
            padding: '14px',
            borderRadius: '8px',
            background: 'rgba(255, 255, 255, 0.03)',
            border: '1px solid var(--border-color)',
            textAlign: 'center',
          }}
        >
          <div style={{ fontSize: '0.75rem', color: '#94a3b8', marginBottom: '6px' }}>
            DEMO PLACEMENT OFFICER CREDENTIALS:
          </div>
          <div style={{ fontSize: '0.8rem', color: '#cbd5e1', fontFamily: 'monospace' }}>
            admin@prepvr.edu &nbsp;|&nbsp; Placement@2026
          </div>
          <button
            type="button"
            onClick={autofillDemo}
            style={{
              marginTop: '10px',
              padding: '4px 12px',
              background: 'rgba(56, 189, 248, 0.1)',
              border: '1px solid rgba(56, 189, 248, 0.25)',
              borderRadius: '6px',
              color: '#38bdf8',
              fontSize: '0.75rem',
              fontWeight: 600,
              cursor: 'pointer',
            }}
          >
            Auto-fill Demo Credentials
          </button>
        </div>
      </div>
    </div>
  );
};
