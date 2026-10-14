# PREP VR — Centralized Student Placement Data System (Backend API)

High-performance, relational Node.js + TypeScript + Express + PostgreSQL + Prisma ORM backend service for institutional monitoring of VR interview simulations.

---

## 1. System Architecture

```
STUDENT (at College VR Headset / Computer)
   ↓
UNITY APPLICATION (PREP VR Interview Simulation)
   ↓ HTTP REST API (POST /api/interviews)
BACKEND API (Node.js + Express + Prisma ORM on port 5000)
   ↓ TCP pgwire
POSTGRESQL DATABASE (Relational schema: Student, Interview, Stages, Feedback)
   ↓ HTTP REST API (GET /api/dashboard/summary, GET /api/rankings)
PLACEMENT WEB DASHBOARD (Next.js 14 + TypeScript on port 3000)
   ↓
PLACEMENT TEAM / FACULTY / DEAN
```

- **Zero Direct Database Exposure**: Neither the placement frontend nor the Unity application ever connect directly to PostgreSQL. All interactions go through the authenticated HTTP REST API.
- **Independent Operations**: The placement department monitors student cohorts, rankings, and deep-dive reports without opening Unity.

---

## 2. Relational Database Schema (PostgreSQL)

The database schema is strictly structured across 5 entities:

### `Student`
- `id` (String, CUID PK)
- `rollNumber` (String, Unique, Indexed) — Identifies student (e.g. `REC001`)
- `name` (String) — Full student name
- `department` (String, Indexed) — e.g. "Computer Science & Engineering"
- `year` (Int) — Default: 4
- `email` (String, Unique, Indexed)

### `Interview`
- `id` (String, CUID PK)
- `studentId` (String FK → `Student.id`)
- `rollNumber` (String, Indexed)
- `interviewDate` (DateTime, Default: now)
- `interviewDuration` (Int, Seconds, e.g. 600)
- `interviewStatus` (Enum: `PENDING`, `IN_PROGRESS`, `COMPLETED`, `FAILED`)
- `overallScore` (Float?, 0 - 100)
- `readinessScore` (Float?, 0 - 100)
- `readinessLevel` (String?)
- `communicationScore` (Float?, 0 - 10)
- `confidenceScore` (Float?, 0 - 10)
- `professionalismScore` (Float?, 0 - 10)
- `behavioralScore` (Float?, 0 - 10)
- `hiringManagerScore` (Float?, 0 - 10)
- `reportUrl` / `reportPath` / `reportIdentifier` (String?)
- `idempotencyKey` (String?, Unique) — Prevents duplicate submissions

### `InterviewStage`
- `id` (String, CUID PK)
- `interviewId` (String FK → `Interview.id`)
- `stageNumber` (Int: 1 = HR, 2 = Behavioral, 3 = Hiring Manager)
- `stageName` (String: "HR Interviewer", "Behavioral Interviewer", "Hiring Manager")
- `durationSeconds` (Int, Default: 200)
- `score` (Float?)
- `questionCount` (Int)
- `answerCount` (Int)
- `transcriptSummary` (Text?)

### `Feedback`
- `id` (String, CUID PK)
- `interviewId` (String FK → `Interview.id`, Unique)
- `overallSummary` (Text)
- `strengths` (String[]) — Verified candidate strengths
- `weaknesses` (String[]) — Priority growth areas
- `recommendations` (String[]) — Actionable placement roadmap
- `observableEyeContact` (Text?) — Factual visual engagement record
- `observablePostureBodyLanguage` (Text?) — Posture stability record
- `observableEngagement` (Text?) — Response latency & cadence

### `PlacementUser`
- `id` (String, CUID PK)
- `email` (String, Unique)
- `name` (String)
- `passwordHash` (String, bcrypt)
- `role` (String: `DIRECTOR_OF_PLACEMENTS`, `COORDINATOR`)

---

## 3. Environment Configuration (`.env`)

Create or update `.env` in the `Backend/` directory:

```env
PORT=5000
NODE_ENV=development

# PostgreSQL Connection String
DATABASE_URL="postgresql://prepvr:prepvr_password@localhost:5432/prepvr_placement?schema=public"

# JWT Secret for Placement Staff Authentication
JWT_SECRET="prepvr_super_secret_jwt_key_placement_2026"
JWT_EXPIRES_IN="7d"

# Allowed CORS Origins
CORS_ORIGIN="http://localhost:3000,http://127.0.0.1:3000"

# Initial Placement Admin Credentials
PLACEMENT_ADMIN_EMAIL="admin@prepvr.edu"
PLACEMENT_ADMIN_PASSWORD="Placement@2026"

# Reports Storage Path
REPORTS_STORAGE_PATH="./reports"
```

---

## 4. How to Start PostgreSQL

### Option A: Local WSL PostgreSQL (Already Configured)
On this machine, PostgreSQL 18 is running as a service in WSL on port 5432:
```bash
wsl -d Ubuntu service postgresql status
# If stopped, start it with:
wsl -d Ubuntu service postgresql start
```

### Option B: Docker Compose
```bash
docker compose up -d
```

---

## 5. How to Run Migrations & Push Schema

```bash
# Generate Prisma Client
npm run db:generate

# Push schema directly to PostgreSQL
npm run db:push
```

---

## 6. How to Seed Demo Data

Seeds 7 realistic student records (`REC001` - `REC007`) with varied statuses (`COMPLETED`, `IN_PROGRESS`, `PENDING`, `FAILED`), competency scores, feedbacks, and sets up the placement staff account (`admin@prepvr.edu`):

```bash
npm run db:seed
```

---

## 7. How to Start the Backend API Server

```bash
# In development (with ts-node):
npm run dev

# Or build and run production bundle:
npm run build
npm start
```
The server will start at `http://localhost:5000`.

---

## 8. API Endpoints Reference

| Method | Endpoint | Description | Auth Required |
|---|---|---|---|
| `GET` | `/api/health` | Service health status | No |
| `POST` | `/api/auth/login` | Placement staff authentication | No |
| `GET` | `/api/auth/me` | Current staff profile | Yes (Bearer JWT) |
| `POST` | `/api/students` | Register student in cohort | No / Internal |
| `GET` | `/api/students` | List all students with search & filters | No |
| `GET` | `/api/students/:id` | Get single student info & interview history | No |
| `GET` | `/api/students/:id/interviews` | Get student's interview attempts | No |
| `POST` | `/api/interviews` | Store completed interview from Unity | No (Unity API) |
| `GET` | `/api/interviews/:id` | Fetch specific interview result | No |
| `GET` | `/api/dashboard/summary` | Placement overview aggregate metrics | No |
| `GET` | `/api/rankings` | Student ranking list ordered by score | No |

---

## 9. Unity Integration Specification (`POST /api/interviews`)

When a student completes an interview in Unity, Unity dispatches an HTTP POST request.

### Endpoint:
`POST http://localhost:5000/api/interviews`  
Header: `Content-Type: application/json`

### Required Fields:
- `rollNumber` (string) — Candidate roll number (e.g. `"REC001"`)

### Optional Fields:
- `interviewDate` (string, ISO-8601) — Defaults to `now()`
- `interviewDuration` (number, seconds) — Defaults to `600`
- `interviewStatus` (string) — `"COMPLETED"`, `"IN_PROGRESS"`, `"PENDING"`, `"FAILED"`. Defaults to `"COMPLETED"`.
- `overallScore` (number) — 0 to 100
- `readinessScore` (number) — 0 to 100
- `readinessLevel` (string) — e.g. `"Placement Ready — Proficient (Strong Potential)"`
- `communicationScore` (number) — 0 to 10
- `confidenceScore` (number) — 0 to 10
- `professionalismScore` (number) — 0 to 10
- `behavioralScore` (number) — 0 to 10
- `hiringManagerScore` (number) — 0 to 10
- `reportUrl` / `reportPath` (string) — Path/URL to generated vector PDF placement report
- `idempotencyKey` (string) — Unique session identifier to prevent duplicate submissions
- `stages` (array):
  ```json
  [
    {
      "stageNumber": 1,
      "stageName": "HR Interviewer",
      "durationSeconds": 200,
      "score": 90,
      "questionCount": 4,
      "answerCount": 4,
      "transcriptSummary": "Reviewed candidate resume background."
    }
  ]
  ```
- `feedback` (object):
  ```json
  {
    "overallSummary": "Candidate demonstrated strong placement readiness...",
    "strengths": ["Clear STAR articulation", "Grounded project understanding"],
    "weaknesses": ["Incorporate quantifiable business impact metrics"],
    "recommendations": ["Practice 2-minute elevator pitch for high concurrency"],
    "observableEyeContact": "Candidate maintained steady forward gaze.",
    "observablePostureBodyLanguage": "Upright, centered sitting posture sustained.",
    "observableEngagement": "Response latency averaged 1.8 seconds."
  }
  ```

### Example Minimal Payload:
```json
{
  "rollNumber": "REC001",
  "interviewDuration": 600,
  "interviewStatus": "COMPLETED",
  "overallScore": 88,
  "readinessScore": 92,
  "communicationScore": 9,
  "confidenceScore": 8.5,
  "professionalismScore": 9,
  "behavioralScore": 9,
  "hiringManagerScore": 8.5,
  "feedback": "Swetha completed the 10-minute session with exemplary poise."
}
```

---

## 10. Automated Verification

Run the automated test suite verifying all 11 API endpoints:
```bash
npm run test:api
```
