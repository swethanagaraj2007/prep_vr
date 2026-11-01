# PREP VR - Consolidated Source Code

This directory provides an organized, unified view of all core source code for the **PREP VR** placement training and interview simulation ecosystem.

---

## Directory Structure

```
Source_Code/
├── Unity_Client/          # VR Interview Simulation Client (Unity / C#)
│   ├── PREPVR/            # Core PREPVR modules (Feedback, Interview, Resume, UI, Data)
│   ├── General_Scripts/   # Scene management and UI helpers
│   └── Editor_Tools/      # LipSync automation & Model blendshape audit utilities
│
├── Backend/               # API & Analytics Server (Node.js, TypeScript, Python)
│   ├── src/               # Controllers, Middleware, Services, Routes, Type definitions
│   ├── report_generation/ # Automated PDF Placement & Performance Report Generator
│   ├── resume_intelligence/ # ATS parser & scoring logic
│   ├── scripts/           # Database seeding and testing scripts
│   ├── prisma/            # PostgreSQL schema definitions
│   ├── package.json       # Backend dependencies
│   └── tsconfig.json      # TypeScript compiler configuration
│
└── Web_Dashboard/         # Placement Officer & Student Web Dashboard (Next.js / React)
    ├── app/               # Next.js App Router pages (Analytics, Candidates, Interviews)
    ├── components/        # UI components (KPI cards, charts, report viewer)
    ├── lib/               # Utility functions and mock data fallback
    ├── package.json       # Dashboard dependencies
    └── tsconfig.json      # Frontend TypeScript configuration
```

---

## Component Highlights

1. **Unity Client (`Unity_Client/`)**:
   - `FeedbackEngine.cs`: Analyzes user response quality, technical accuracy, confidence, and speech pacing.
   - `InterviewOrchestrator.cs`: Coordinates real-time AI avatar interactions, audio synthesis, and turn-taking.
   - `InterviewAvatarMouthController.cs`: Real-time jaw and mouth blendshape animation driven by voice volume.
   - `ResumeUploader.cs`: Syncs candidate resumes directly with the backend API.

2. **Backend Services (`Backend/`)**:
   - `src/controllers/`: Handles authentication, student placement records, and live interview sessions.
   - `generate_feedback_pdf.py`: Python ReportLab script generating professional multi-page placement feedback reports.

3. **Web Dashboard (`Web_Dashboard/`)**:
   - Modern Next.js interface for training & placement officers to monitor candidate scores, readiness levels, and download comprehensive feedback PDFs.
