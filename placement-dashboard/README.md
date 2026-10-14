# PREP VR — Placement Web Dashboard

The official web portal for college placement officers, faculty coordinators, and leadership to monitor student VR interview completion, performance rankings, competency evaluations, and institutional analytics without needing to open Unity.

---

## Features

1. **Secure Placement Staff Authentication**:
   - Placement directorate/faculty login via email & password.
   - Initial demo credentials:
     - **Email**: `admin@prepvr.edu`
     - **Password**: `Placement@2026`
   - One-click demo autofill button for instant testing.

2. **Placement Overview KPI Cards**:
   - **Total Enrolled**: Total students registered in the cohort.
   - **Completed VR Tests**: Number of candidates who completed the 10-minute 3-stage interview + completion percentage.
   - **Pending / Active**: Students waiting for headset slots or currently active in VR.
   - **Average Readiness %**: Institutional placement readiness score.
   - **Top Score / Average Score**: Highest scoring candidate and overall cohort mean.

3. **Student Ranking & Directory Table**:
   - Search by roll number, student name, or department.
   - Filter by Department (CSE, IT, AI & DS, ECE, Mechanical).
   - Filter by Interview Status (`COMPLETED`, `PENDING`, `IN_PROGRESS`, `FAILED`).
   - Dynamic sorting by Rank, Readiness Score, Overall Score, or Interview Date.
   - Visual score progress bars and color-coded status badges.
   - Direct button to open deep-dive candidate analysis.

4. **Student Details Modal (Deep-Dive Analysis)**:
   - Candidate metadata (Roll Number, Name, Department, Year, Email).
   - Institutional Readiness Tier Badge (e.g. *Placement Ready — Exemplary Tier 1*).
   - 5 Core Placement Competency Gauges (Communication, Confidence, Professionalism, Behavioral, Hiring Manager).
   - 10-Minute 3-Stage Panel Execution Breakdown (Stage 1: HR, Stage 2: Behavioral, Stage 3: Hiring Manager, with question/answer counts and duration).
   - Evidence-based qualitative feedback:
     - Executive summary
     - Verified candidate strengths
     - Priority growth areas
     - Actionable placement preparation roadmap
     - Factual observable demeanor (visual gaze, posture stability, response latency)
   - PDF Assessment Report link/preview button.
   - Attempt history selector (for candidates with multiple mock rounds).

5. **Register Student Modal**:
   - Direct UI for placement coordinators to enroll new roll numbers into the cohort.

---

## How to Run Locally

```bash
cd placement-dashboard

# Install dependencies
npm install

# Start development server
npm run dev
```

Visit: `http://localhost:3000`

---

## Environment Variables (`.env.local`)

```env
NEXT_PUBLIC_API_URL=http://localhost:5000/api
NEXT_PUBLIC_APP_NAME="PREP VR Placement Central"
```
