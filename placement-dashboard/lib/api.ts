const API_BASE = process.env.NEXT_PUBLIC_API_URL || 'http://localhost:5000/api';

export interface DashboardSummary {
  totalStudents: number;
  completedInterviews: number;
  pendingInterviews: number;
  inProgressInterviews: number;
  failedInterviews: number;
  averageScore: number;
  averageReadiness: number;
  highestScore: number;
  departmentBreakdown: Array<{
    department: string;
    totalStudents: number;
    completedInterviews: number;
    averageScore: number;
    averageReadiness: number;
  }>;
}

export interface StudentRanking {
  rank: number;
  studentId: string;
  rollNumber: string;
  studentName: string;
  department: string;
  year: number;
  interviewId?: string;
  interviewStatus: 'COMPLETED' | 'PENDING' | 'IN_PROGRESS' | 'FAILED';
  overallScore: number | null;
  readinessScore: number | null;
  readinessLevel: string | null;
  communicationScore: number | null;
  confidenceScore: number | null;
  professionalismScore: number | null;
  behavioralScore: number | null;
  hiringManagerScore: number | null;
  interviewDate: string | null;
  interviewDuration: number | null;
  hasReport: boolean;
}

export interface InterviewStage {
  id: string;
  stageNumber: number;
  stageName: string;
  durationSeconds: number;
  score: number | null;
  questionCount: number;
  answerCount: number;
  transcriptSummary: string | null;
}

export interface FeedbackData {
  id: string;
  overallSummary: string;
  strengths: string[];
  weaknesses: string[];
  recommendations: string[];
  observableEyeContact?: string;
  observablePostureBodyLanguage?: string;
  observableEngagement?: string;
}

export interface DetailedInterview {
  id: string;
  rollNumber: string;
  interviewDate: string;
  interviewDuration: number;
  interviewStatus: 'COMPLETED' | 'PENDING' | 'IN_PROGRESS' | 'FAILED';
  overallScore: number | null;
  readinessScore: number | null;
  readinessLevel: string | null;
  communicationScore: number | null;
  confidenceScore: number | null;
  professionalismScore: number | null;
  behavioralScore: number | null;
  hiringManagerScore: number | null;
  reportUrl: string | null;
  reportPath: string | null;
  reportIdentifier: string | null;
  stages: InterviewStage[];
  feedback?: FeedbackData | null;
}

export interface DetailedStudent {
  id: string;
  rollNumber: string;
  name: string;
  department: string;
  year: number;
  email: string;
  createdAt: string;
  updatedAt: string;
  interviews: DetailedInterview[];
}

export async function getDashboardSummary(): Promise<DashboardSummary> {
  const res = await fetch(`${API_BASE}/dashboard/summary`, { cache: 'no-store' });
  if (!res.ok) throw new Error('Failed to fetch dashboard summary');
  const json = await res.json();
  return json.data;
}

export async function getRankings(params?: {
  department?: string;
  status?: string;
  limit?: number;
}): Promise<StudentRanking[]> {
  const searchParams = new URLSearchParams();
  if (params?.department && params.department !== 'All') searchParams.set('department', params.department);
  if (params?.status && params.status !== 'All') searchParams.set('status', params.status);
  if (params?.limit) searchParams.set('limit', String(params.limit));

  const url = `${API_BASE}/rankings?${searchParams.toString()}`;
  const res = await fetch(url, { cache: 'no-store' });
  if (!res.ok) throw new Error('Failed to fetch student rankings');
  const json = await res.json();
  return json.data;
}

export async function getStudentById(idOrRoll: string): Promise<DetailedStudent> {
  const res = await fetch(`${API_BASE}/students/${idOrRoll}`, { cache: 'no-store' });
  if (!res.ok) throw new Error(`Failed to fetch student: ${idOrRoll}`);
  const json = await res.json();
  return json.data;
}

export async function createStudent(data: {
  rollNumber: string;
  name: string;
  department: string;
  year?: number;
  email: string;
}) {
  const res = await fetch(`${API_BASE}/students`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data),
  });
  const json = await res.json();
  if (!res.ok) throw new Error(json.error || 'Failed to register student');
  return json.data;
}

export async function loginStaff(email: string, password: string) {
  const res = await fetch(`${API_BASE}/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password }),
  });
  const json = await res.json();
  if (!res.ok) throw new Error(json.error || 'Login failed');
  return json.data;
}

export async function getStudents(params?: {
  search?: string;
  department?: string;
  status?: string;
  sortBy?: string;
  sortOrder?: 'asc' | 'desc';
}) {
  const searchParams = new URLSearchParams();
  if (params?.search) searchParams.set('search', params.search);
  if (params?.department && params.department !== 'All') searchParams.set('department', params.department);
  if (params?.status && params.status !== 'All') searchParams.set('status', params.status);
  if (params?.sortBy) searchParams.set('sortBy', params.sortBy);
  if (params?.sortOrder) searchParams.set('sortOrder', params.sortOrder);

  const url = `${API_BASE}/students?${searchParams.toString()}`;
  const res = await fetch(url, { cache: 'no-store' });
  if (!res.ok) throw new Error('Failed to fetch students list');
  const json = await res.json();
  return json.data;
}

export async function updateStudent(id: string, data: Partial<{
  rollNumber: string;
  name: string;
  department: string;
  year: number;
  email: string;
}>) {
  const res = await fetch(`${API_BASE}/students/${id}`, {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data),
  });
  const json = await res.json();
  if (!res.ok) throw new Error(json.error || 'Failed to update student');
  return json.data;
}

export async function deleteStudent(id: string) {
  const res = await fetch(`${API_BASE}/students/${id}`, {
    method: 'DELETE',
  });
  const json = await res.json();
  if (!res.ok) throw new Error(json.error || 'Failed to delete student');
  return json.data;
}

export async function importCsvStudents(data: { csvData?: string; students?: any[] }) {
  const res = await fetch(`${API_BASE}/students/import-csv`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(data),
  });
  const json = await res.json();
  if (!res.ok) throw new Error(json.error || 'Failed to import CSV');
  return json.data;
}

export async function getInterviews(params?: {
  status?: string;
  department?: string;
  search?: string;
  sortBy?: string;
  sortOrder?: 'asc' | 'desc';
  limit?: number;
  offset?: number;
}) {
  const searchParams = new URLSearchParams();
  if (params?.status && params.status !== 'All') searchParams.set('status', params.status);
  if (params?.department && params.department !== 'All') searchParams.set('department', params.department);
  if (params?.search) searchParams.set('search', params.search);
  if (params?.sortBy) searchParams.set('sortBy', params.sortBy);
  if (params?.sortOrder) searchParams.set('sortOrder', params.sortOrder);
  if (params?.limit) searchParams.set('limit', String(params.limit));
  if (params?.offset) searchParams.set('offset', String(params.offset));

  const url = `${API_BASE}/interviews?${searchParams.toString()}`;
  const res = await fetch(url, { cache: 'no-store' });
  if (!res.ok) throw new Error('Failed to fetch interviews');
  const json = await res.json();
  return { interviews: json.data as DetailedInterview[], total: json.total as number };
}

export function getExportCsvUrl(type: 'rankings' | 'students' | 'sessions' | 'department', params?: any) {
  const searchParams = new URLSearchParams({ type });
  if (params?.department && params.department !== 'All') searchParams.set('department', params.department);
  if (params?.status && params.status !== 'All') searchParams.set('status', params.status);
  return `${API_BASE}/reports/export-csv?${searchParams.toString()}`;
}

