'use client';

import React from 'react';
import { useAuth } from '../lib/auth-context';
import {
  LogOut,
  User,
  ShieldCheck,
  Award,
  LayoutDashboard,
  Users,
  Trophy,
  Video,
  FileSpreadsheet,
  RotateCw
} from 'lucide-react';

export type NavTabType = 'overview' | 'students' | 'rankings' | 'sessions' | 'reports';

interface NavbarProps {
  activeTab: NavTabType;
  onSelectTab: (tab: NavTabType) => void;
  onRefresh?: () => void;
  isRefreshing?: boolean;
}

export const Navbar: React.FC<NavbarProps> = ({
  activeTab,
  onSelectTab,
  onRefresh,
  isRefreshing,
}) => {
  const { user, logout } = useAuth();

  const navItems: Array<{ id: NavTabType; label: string; icon: React.ReactNode }> = [
    { id: 'overview', label: 'Dashboard', icon: <LayoutDashboard size={16} /> },
    { id: 'students', label: 'Students', icon: <Users size={16} /> },
    { id: 'rankings', label: 'Leaderboard', icon: <Trophy size={16} /> },
    { id: 'sessions', label: 'VR Sessions', icon: <Video size={16} /> },
    { id: 'reports', label: 'Reports', icon: <FileSpreadsheet size={16} /> },
  ];

  return (
    <header style={{ margin: '16px 24px 20px 24px' }}>
      {/* Top Banner */}
      <div
        className="glass-panel"
        style={{
          padding: '12px 24px',
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          flexWrap: 'wrap',
          gap: '16px',
        }}
      >
        {/* Brand */}
        <div style={{ display: 'flex', alignItems: 'center', gap: '14px' }}>
          <div
            style={{
              width: '40px',
              height: '40px',
              borderRadius: '10px',
              background: 'linear-gradient(135deg, #0284c7 0%, #38bdf8 100%)',
              display: 'flex',
              alignItems: 'center',
              justifyContent: 'center',
              boxShadow: '0 4px 14px rgba(2, 132, 199, 0.4)',
            }}
          >
            <Award size={22} color="#ffffff" />
          </div>
          <div>
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
              <h1 className="brand-font" style={{ fontSize: '1.2rem', fontWeight: 700, letterSpacing: '-0.02em', color: '#f8fafc' }}>
                PREP VR
              </h1>
              <span
                style={{
                  fontSize: '0.7rem',
                  fontWeight: 600,
                  padding: '2px 8px',
                  borderRadius: '6px',
                  background: 'rgba(2, 132, 199, 0.2)',
                  color: '#38bdf8',
                  border: '1px solid rgba(56, 189, 248, 0.3)',
                }}
              >
                PLACEMENT PORTAL
              </span>
            </div>
            <p style={{ fontSize: '0.78rem', color: '#94a3b8' }}>
              Institutional VR Mock Interview Simulation & Student Readiness Analytics
            </p>
          </div>
        </div>

        {/* Right Controls: Refresh, Live status, User */}
        <div style={{ display: 'flex', alignItems: 'center', gap: '14px' }}>
          {onRefresh && (
            <button
              onClick={onRefresh}
              disabled={isRefreshing}
              className="btn-secondary"
              title="Refresh Data"
              style={{ padding: '7px 12px', fontSize: '0.8rem' }}
            >
              <RotateCw size={14} className={isRefreshing ? 'spin' : ''} color="#38bdf8" />
              <span>{isRefreshing ? 'Syncing...' : 'Sync'}</span>
            </button>
          )}

          {/* Database Live Indicator */}
          <div
            style={{
              display: 'flex',
              alignItems: 'center',
              gap: '8px',
              padding: '6px 12px',
              borderRadius: '20px',
              background: 'rgba(16, 185, 129, 0.1)',
              border: '1px solid rgba(16, 185, 129, 0.25)',
            }}
          >
            <div className="pulse-indicator" style={{ background: '#10b981' }} />
            <span style={{ fontSize: '0.72rem', fontWeight: 600, color: '#34d399' }}>POSTGRESQL LIVE</span>
          </div>

          {/* User Profile */}
          {user && (
            <div style={{ display: 'flex', alignItems: 'center', gap: '10px', paddingLeft: '10px', borderLeft: '1px solid var(--border-color)' }}>
              <div
                style={{
                  width: '34px',
                  height: '34px',
                  borderRadius: '50%',
                  background: 'rgba(255, 255, 255, 0.08)',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  border: '1px solid rgba(255, 255, 255, 0.15)',
                }}
              >
                <User size={16} color="#94a3b8" />
              </div>
              <div style={{ textAlign: 'left' }}>
                <div style={{ fontSize: '0.82rem', fontWeight: 600, color: '#f1f5f9' }}>
                  {user.name}
                </div>
                <div style={{ fontSize: '0.68rem', color: '#64748b', display: 'flex', alignItems: 'center', gap: '3px' }}>
                  <ShieldCheck size={11} color="#0284c7" />
                  {user.role.replace(/_/g, ' ')}
                </div>
              </div>

              <button
                onClick={logout}
                title="Sign Out"
                className="btn-secondary"
                style={{ padding: '6px 8px', marginLeft: '4px' }}
              >
                <LogOut size={14} />
              </button>
            </div>
          )}
        </div>
      </div>

      {/* Navigation Tabs Bar */}
      <nav
        style={{
          display: 'flex',
          gap: '8px',
          marginTop: '12px',
          padding: '4px',
          background: 'rgba(15, 23, 42, 0.7)',
          borderRadius: '12px',
          border: '1px solid var(--border-color)',
          width: 'fit-content',
        }}
      >
        {navItems.map((item) => {
          const isActive = activeTab === item.id;
          return (
            <button
              key={item.id}
              onClick={() => onSelectTab(item.id)}
              style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: '8px',
                padding: '8px 18px',
                borderRadius: '8px',
                fontSize: '0.85rem',
                fontWeight: isActive ? 600 : 500,
                border: 'none',
                cursor: 'pointer',
                transition: 'all 0.15s ease',
                background: isActive
                  ? 'linear-gradient(135deg, #0284c7 0%, #0369a1 100%)'
                  : 'transparent',
                color: isActive ? '#ffffff' : '#94a3b8',
                boxShadow: isActive ? '0 4px 12px rgba(2, 132, 199, 0.3)' : 'none',
              }}
            >
              {item.icon}
              <span>{item.label}</span>
            </button>
          );
        })}
      </nav>
    </header>
  );
};
