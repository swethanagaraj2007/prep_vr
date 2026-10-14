export type InterviewStatusType = 'PENDING' | 'IN_PROGRESS' | 'COMPLETED' | 'FAILED';

export interface CreateStudentInput {
  rollNumber: string;
  name: string;
  department: string;
  year?: number;
  email: string;
}

export interface StageInput {
  stageNumber: number;
  stageName: string;
  durationSeconds?: number;
  score?: number;
  questionCount?: number;
  answerCount?: number;
  transcriptSummary?: string;
}

export interface FeedbackInput {
  overallSummary?: string;
  strengths?: string[];
  weaknesses?: string[];
  recommendations?: string[];
  observableEyeContact?: string;
  observablePostureBodyLanguage?: string;
  observableEngagement?: string;
}

export interface CreateInterviewInput {
  studentId?: string;
  rollNumber: string;
  studentName?: string;
  department?: string;
  interviewDate?: string;
  interviewDuration?: number; // seconds (default: 600)
  interviewStatus?: InterviewStatusType;
  overallScore?: number;
  readinessScore?: number;
  readinessLevel?: string;
  communicationScore?: number;
  confidenceScore?: number;
  professionalismScore?: number;
  behavioralScore?: number;
  hiringManagerScore?: number;
  feedback?: FeedbackInput | string;
  reportUrl?: string;
  reportPath?: string;
  reportIdentifier?: string;
  stages?: StageInput[];
  idempotencyKey?: string;
}

export interface DashboardSummaryResponse {
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

export interface RankingItem {
  rank: number;
  studentId: string;
  rollNumber: string;
  studentName: string;
  department: string;
  year: number;
  interviewId?: string;
  interviewStatus: InterviewStatusType;
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
