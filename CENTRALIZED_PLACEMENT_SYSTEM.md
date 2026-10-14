# PREP VR — Centralized Student Placement Data System

## 1. Executive Summary

This system establishes the centralized data architecture for the **PREP VR** Virtual Reality Interview Simulation project.

In college deployments, hundreds of students independently complete 10-minute, 3-stage VR interviews on college VR headsets and computers. The placement department never needs to open Unity. Instead, student interview results, competency scores, readiness ratings, and qualitative feedback are recorded centrally in PostgreSQL and visualized on a modern, institutional **Placement Web Dashboard**.

---

## 2. Architecture & Communication Flow

```
                      STUDENT
                         ↓
                 UNITY APPLICATION
      (10-minute, 3-interviewer VR simulation)
                         ↓
               HTTP REST API (POST)
                         ↓
                    BACKEND API
           (Node.js + Express + Prisma)
                    [Port 5000]
                         ↓
                POSTGRESQL DATABASE
   (Structured Schema: Student, Interview, Stages, Feedback)
                    [Port 5432]
                         ↓
               HTTP REST API (GET)
                         ↓
              PLACEMENT WEB DASHBOARD
              (Next.js 14 + TypeScript)
                    [Port 3000]
                         ↓
            PLACEMENT TEAM / FACULTY / DEAN
```

### Architectural Principles:
1. **No Direct Database Access**: The Placement Dashboard and Unity NEVER talk directly to PostgreSQL. All interactions go through the authenticated REST API.
2. **Preservation of Unity**: The existing Unity simulation, Convai characters, 3-stage interview flow, and resume parser remain 100% untouched and intact.
3. **Structured Relational Storage**: Critical values (scores, statuses, stages, feedback) are stored in dedicated relational columns, enabling high-performance filtering, sorting, and institutional ranking across hundreds of students.

---

## 3. Database Schema (`prisma/schema.prisma`)

```prisma
enum InterviewStatus {
  PENDING
  IN_PROGRESS
  COMPLETED
  FAILED
}

model Student {
  id          String      @id @default(cuid())
  rollNumber  String      @unique
  name        String
  department  String
  year        Int         @default(4)
  email       String      @unique
  createdAt   DateTime    @default(now())
  updatedAt   DateTime    @updatedAt
  interviews  Interview[]

  @@index([department])
  @@index([rollNumber])
}

model Interview {
  id                    String          @id @default(cuid())
  studentId             String
  student               Student         @relation(fields: [studentId], references: [id], onDelete: Cascade)
  rollNumber            String
  interviewDate         DateTime        @default(now())
  interviewDuration     Int             @default(600) // Seconds
  interviewStatus       InterviewStatus @default(PENDING)
  
  overallScore          Float?          // 0 - 100
  readinessScore        Float?          // 0 - 100
  readinessLevel        String?
  
  communicationScore    Float?          // 0 - 10
  confidenceScore       Float?          // 0 - 10
  professionalismScore  Float?          // 0 - 10
  behavioralScore       Float?          // 0 - 10
  hiringManagerScore    Float?          // 0 - 10
  
  reportUrl             String?
  reportPath            String?
  reportIdentifier      String?
  idempotencyKey        String?         @unique

  createdAt             DateTime        @default(now())
  updatedAt             DateTime        @updatedAt

  stages                InterviewStage[]
  feedback              Feedback?

  @@index([studentId])
  @@index([rollNumber])
  @@index([interviewStatus])
  @@index([overallScore])
  @@index([readinessScore])
  @@index([interviewDate])
}

model InterviewStage {
  id                String    @id @default(cuid())
  interviewId       String
  interview         Interview @relation(fields: [interviewId], references: [id], onDelete: Cascade)
  stageNumber       Int       // 1 = HR, 2 = Behavioral, 3 = Hiring Manager
  stageName         String
  durationSeconds   Int       @default(200)
  score             Float?
  questionCount     Int       @default(0)
  answerCount       Int       @default(0)
  transcriptSummary String?   @db.Text
  createdAt         DateTime  @default(now())

  @@unique([interviewId, stageNumber])
  @@index([interviewId])
}

model Feedback {
  id                            String    @id @default(cuid())
  interviewId                   String    @unique
  interview                     Interview @relation(fields: [interviewId], references: [id], onDelete: Cascade)
  overallSummary                String    @db.Text
  strengths                     String[]
  weaknesses                    String[]
  recommendations               String[]
  observableEyeContact          String?   @db.Text
  observablePostureBodyLanguage String?   @db.Text
  observableEngagement          String?   @db.Text
  createdAt                     DateTime  @default(now())
  updatedAt                     DateTime  @updatedAt
}

model PlacementUser {
  id           String   @id @default(cuid())
  email        String   @unique
  name         String
  passwordHash String
  role         String   @default("PLACEMENT_OFFICER")
  createdAt    DateTime @default(now())
  updatedAt    DateTime @updatedAt
}
```

---

## 4. API Endpoints Reference

| Method | Route | Description |
|---|---|---|
| `GET` | `/api/health` | Health check endpoint |
| `POST` | `/api/auth/login` | Staff login with email and password |
| `GET` | `/api/auth/me` | Current staff profile (JWT protected) |
| `POST` | `/api/students` | Register a new student |
| `GET` | `/api/students` | List all students with search & filter |
| `GET` | `/api/students/:id` | Student profile + full interview history |
| `GET` | `/api/students/:id/interviews` | All interview attempts for a student |
| `POST` | `/api/interviews` | Store completed interview from Unity |
| `GET` | `/api/interviews/:id` | Retrieve single interview result |
| `GET` | `/api/dashboard/summary` | Aggregate placement overview metrics |
| `GET` | `/api/rankings` | Student ranking list ordered by readiness |

---

## 5. Placement Web Dashboard Capabilities

- **URL**: `http://localhost:3000`
- **Login Credentials**:
  - Email: `admin@prepvr.edu`
  - Password: `Placement@2026`
  - Includes 1-click **Auto-fill Demo Credentials** button for instant testing.
- **KPI Metrics Overview**:
  - Total Enrolled Students
  - Completed VR Interviews (+ completion rate percentage)
  - Pending / Active Sessions
  - Average Institutional Readiness Score
  - Top Candidate Score and Cohort Average
- **Interactive Directory & Ranking Table**:
  - Instant live search (Roll number, Student name, Department)
  - Department filtering (CSE, IT, AI&DS, ECE, Mechanical)
  - Status filtering (`COMPLETED`, `PENDING`, `IN_PROGRESS`, `FAILED`)
  - Sorting by Rank (#1, #2...), Readiness Score, Overall Score, or Date
  - Visual score progress bars & status badges
- **Candidate Deep-Dive Modal**:
  - Full student information
  - Institutional Readiness Tier Banner (e.g. *Exemplary — Tier 1*)
  - 5-Competency Score Cards (Communication, Confidence, Professionalism, Behavioral, Hiring Manager)
  - 3-Stage Progress details (HR, Behavioral, Hiring Manager) with duration, question count, and transcript summaries
  - Evidence-based qualitative feedback (Executive Summary, Verified Strengths, Priority Growth Areas, Actionable Recommendations, Demeanor)
  - PDF Placement Report viewer/download button
  - Multi-attempt interview selector
- **Register Candidate Modal**:
  - Enroll new students into the cohort directly from the web interface.

---

## 6. Seed Data (At Least 5 Students Included)

The database includes 7 realistic seed students:
1. **`REC005` — Priya Natarajan** (`COMPLETED`, Score: 94.5, Readiness: 97.0%, Rank #1)
2. **`REC001` — Swetha Ramachandran** (`COMPLETED`, Score: 88.0, Readiness: 92.0%, Rank #2)
3. **`REC002` — Rahul Verma** (`COMPLETED`, Score: 82.0, Readiness: 85.0%, Rank #3)
4. **`REC006` — Dinesh Kumar** (`COMPLETED`, Score: 74.0, Readiness: 76.5%, Rank #4)
5. **`REC003` — Ananya Iyer** (`IN_PROGRESS`, Active in VR Stage 2)
6. **`REC004` — Karthik Subramanian** (`PENDING`, Waiting for headset slot)
7. **`REC007` — Sneha Mukhopadhyay** (`FAILED`, Headset session interrupted)

---

## 7. How to Run the System

### Step 1: Ensure PostgreSQL is Active
```bash
wsl -d Ubuntu service postgresql status
# If not running:
wsl -d Ubuntu service postgresql start
```

### Step 2: Start the Backend API (Port 5000)
```bash
cd Backend
npm start
# Server listens at http://localhost:5000
```

### Step 3: Start the Placement Dashboard (Port 3000)
```bash
cd placement-dashboard
npm run dev
# Dashboard available at http://localhost:3000
```

### Step 4: Run Automated Verification Tests
```bash
cd Backend
npm run test:api
# Output: RESULTS: 11 PASSED, 0 FAILED
```

---

## 8. Unity → Backend Integration Specification

### Endpoint:
`POST http://localhost:5000/api/interviews`  
`Content-Type: application/json`

### Payload Schema:
```json
{
  "rollNumber": "REC001",
  "interviewDuration": 600,
  "interviewStatus": "COMPLETED",
  "overallScore": 88.0,
  "readinessScore": 92.0,
  "readinessLevel": "Placement Ready — Exemplary (Tier 1)",
  "communicationScore": 9.0,
  "confidenceScore": 8.5,
  "professionalismScore": 9.0,
  "behavioralScore": 9.0,
  "hiringManagerScore": 8.5,
  "reportUrl": "/reports/PREP_VR_Placement_Report_REC001.pdf",
  "idempotencyKey": "SESSION_20261004_REC001_8821",
  "stages": [
    {
      "stageNumber": 1,
      "stageName": "HR Interviewer",
      "durationSeconds": 200,
      "score": 90,
      "questionCount": 4,
      "answerCount": 4,
      "transcriptSummary": "Reviewed candidate resume background."
    },
    {
      "stageNumber": 2,
      "stageName": "Behavioral Interviewer",
      "durationSeconds": 200,
      "score": 90,
      "questionCount": 4,
      "answerCount": 4,
      "transcriptSummary": "Evaluated team dynamics and pressure handling."
    },
    {
      "stageNumber": 3,
      "stageName": "Hiring Manager",
      "durationSeconds": 200,
      "score": 85,
      "questionCount": 4,
      "answerCount": 4,
      "transcriptSummary": "Deep dive into system architecture and code ownership."
    }
  ],
  "feedback": {
    "overallSummary": "Candidate demonstrated strong placement readiness...",
    "strengths": [
      "Deep architectural understanding of project components.",
      "Clear, assertive ownership statements during project explanations."
    ],
    "weaknesses": [
      "Incorporate quantifiable business impact metrics (e.g. latency percentiles)."
    ],
    "recommendations": [
      "Practice 2-minute elevator pitch for high concurrency."
    ],
    "observableEyeContact": "Candidate maintained steady forward gaze towards interview panel.",
    "observablePostureBodyLanguage": "Upright, centered sitting posture sustained.",
    "observableEngagement": "Response latency averaged 1.8 seconds."
  }
}
```

### Exact Remaining Work for Unity → Backend Integration (Future Step):
1. In `Assets/PREPVR/Scripts/Feedback/InterviewOverController.cs` or `FeedbackPdfGenerator.cs`:
   - Add a lightweight `UnityWebRequest` coroutine that serializes `FeedbackData` into the JSON structure above and posts to `http://localhost:5000/api/interviews`.
   - The backend automatically auto-provisions the student if their roll number has not been pre-registered, and protects against duplicate submissions using `idempotencyKey`.
2. No modification to Unity's Convai logic, 3-character panel flow, or resume parser is needed.
