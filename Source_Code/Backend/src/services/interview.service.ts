import prisma from '../prisma/client';
import { CreateInterviewInput, InterviewStatusType } from '../types';
import { AppError } from '../middleware/errorHandler.middleware';

export class InterviewService {
  /**
   * Record a completed or updated interview.
   * Supports idempotency and auto-creates student if not pre-registered.
   */
  static async createInterview(data: CreateInterviewInput) {
    if (!data.rollNumber && !data.studentId) {
      throw new AppError('Roll number or studentId is required to record an interview.', 400);
    }

    const rollNumberClean = (data.rollNumber || '').trim().toUpperCase();

    // 1. Resolve or Auto-Provision Student
    let student = await prisma.student.findFirst({
      where: {
        OR: [
          ...(rollNumberClean ? [{ rollNumber: rollNumberClean }] : []),
          ...(data.studentId ? [{ id: data.studentId }] : []),
        ],
      },
    });

    if (!student) {
      if (!rollNumberClean) {
        throw new AppError(`Student with ID '${data.studentId}' not found.`, 404);
      }

      // Auto-register student if Unity submits directly with a roll number
      student = await prisma.student.create({
        data: {
          rollNumber: rollNumberClean,
          name: data.studentName || `Candidate ${rollNumberClean}`,
          department: data.department || 'Computer Science & Engineering',
          year: 4,
          email: `${rollNumberClean.toLowerCase()}@college.edu`,
        },
      });
      console.log(`[InterviewService] Auto-provisioned student record for: ${rollNumberClean} (${student.name})`);
    } else if (data.studentName && (student.name.startsWith('Candidate ') || student.name.trim() === 'Candidate')) {
      student = await prisma.student.update({
        where: { id: student.id },
        data: {
          name: data.studentName,
          ...(data.department ? { department: data.department } : {}),
        },
      });
      console.log(`[InterviewService] Updated student name to: ${data.studentName} for roll: ${rollNumberClean}`);
    }

    // 2. Idempotency Check
    if (data.idempotencyKey) {
      const existing = await prisma.interview.findUnique({
        where: { idempotencyKey: data.idempotencyKey },
        include: { stages: true, feedback: true },
      });
      if (existing) {
        console.log(`[InterviewService] Idempotent replay for key: ${data.idempotencyKey}`);
        return existing;
      }
    }

    // 3. Duplicate Protection (detect duplicate submissions in last 15 seconds)
    const fifteenSecondsAgo = new Date(Date.now() - 15000);
    const recentDuplicate = await prisma.interview.findFirst({
      where: {
        studentId: student.id,
        createdAt: { gte: fifteenSecondsAgo },
        overallScore: data.overallScore ?? undefined,
        interviewStatus: (data.interviewStatus as any) || 'COMPLETED',
      },
      include: { stages: true, feedback: true },
    });
    if (recentDuplicate) {
      console.log(`[InterviewService] Duplicate suppressed for roll: ${student.rollNumber}`);
      return recentDuplicate;
    }

    // 4. Normalise Feedback
    let feedbackObj: any = null;
    if (typeof data.feedback === 'string') {
      feedbackObj = {
        overallSummary: data.feedback,
        strengths: [],
        weaknesses: [],
        recommendations: [],
      };
    } else if (data.feedback && typeof data.feedback === 'object') {
      feedbackObj = {
        overallSummary: data.feedback.overallSummary || 'Interview completed in PREP VR.',
        strengths: data.feedback.strengths || [],
        weaknesses: data.feedback.weaknesses || [],
        recommendations: data.feedback.recommendations || [],
        observableEyeContact: data.feedback.observableEyeContact || null,
        observablePostureBodyLanguage: data.feedback.observablePostureBodyLanguage || null,
        observableEngagement: data.feedback.observableEngagement || null,
      };
    }

    // 5. Normalise Readiness Level
    let readinessLevel = data.readinessLevel;
    if (!readinessLevel && data.readinessScore != null) {
      if (data.readinessScore >= 90) readinessLevel = 'Placement Ready — Exemplary (Tier 1)';
      else if (data.readinessScore >= 75) readinessLevel = 'Placement Ready — Proficient (Strong Potential)';
      else if (data.readinessScore >= 60) readinessLevel = 'Placement Developing — Moderate Focus Needed';
      else readinessLevel = 'Placement Needs Focus — Foundational Preparation Required';
    }

    // 6. Stages data
    const stagesData = (data.stages && data.stages.length > 0)
      ? data.stages.map((st, idx) => ({
          stageNumber: st.stageNumber || idx + 1,
          stageName: st.stageName || (idx === 0 ? 'HR Interviewer' : idx === 1 ? 'Behavioral Interviewer' : 'Hiring Manager'),
          durationSeconds: st.durationSeconds ?? 200,
          score: st.score ?? null,
          questionCount: st.questionCount ?? 0,
          answerCount: st.answerCount ?? 0,
          transcriptSummary: st.transcriptSummary || null,
        }))
      : [
          {
            stageNumber: 1,
            stageName: 'HR Interviewer',
            durationSeconds: 200,
            score: data.communicationScore != null ? data.communicationScore * 10 : null,
            questionCount: 4,
            answerCount: 4,
            transcriptSummary: 'Resume background and career motivations reviewed.',
          },
          {
            stageNumber: 2,
            stageName: 'Behavioral Interviewer',
            durationSeconds: 200,
            score: data.behavioralScore != null ? data.behavioralScore * 10 : null,
            questionCount: 4,
            answerCount: 4,
            transcriptSummary: 'Team dynamics, adaptability, and handling pressure evaluated.',
          },
          {
            stageNumber: 3,
            stageName: 'Hiring Manager',
            durationSeconds: 200,
            score: data.hiringManagerScore != null ? data.hiringManagerScore * 10 : null,
            questionCount: 4,
            answerCount: 4,
            transcriptSummary: 'Project architecture, technical depth, and business impact assessed.',
          },
        ];

    const interviewStatus = (data.interviewStatus as any) || 'COMPLETED';

    // 7. Persist Transaction
    const interview = await prisma.interview.create({
      data: {
        studentId: student.id,
        rollNumber: student.rollNumber,
        interviewDate: data.interviewDate ? new Date(data.interviewDate) : new Date(),
        interviewDuration: data.interviewDuration ?? 600,
        interviewStatus: interviewStatus,
        overallScore: data.overallScore ?? null,
        readinessScore: data.readinessScore ?? null,
        readinessLevel: readinessLevel || null,
        communicationScore: data.communicationScore ?? null,
        confidenceScore: data.confidenceScore ?? null,
        professionalismScore: data.professionalismScore ?? null,
        behavioralScore: data.behavioralScore ?? null,
        hiringManagerScore: data.hiringManagerScore ?? null,
        reportUrl: data.reportUrl || null,
        reportPath: data.reportPath || null,
        reportIdentifier: data.reportIdentifier || null,
        idempotencyKey: data.idempotencyKey || null,
        stages: {
          create: stagesData,
        },
        ...(feedbackObj
          ? {
              feedback: {
                create: feedbackObj,
              },
            }
          : {}),
      },
      include: {
        stages: {
          orderBy: { stageNumber: 'asc' },
        },
        feedback: true,
        student: true,
      },
    });

    return interview;
  }

  /**
   * Get single interview by ID
   */
  static async getInterviewById(interviewId: string) {
    const interview = await prisma.interview.findUnique({
      where: { id: interviewId },
      include: {
        student: true,
        stages: {
          orderBy: { stageNumber: 'asc' },
        },
        feedback: true,
      },
    });

    if (!interview) {
      throw new AppError(`Interview '${interviewId}' not found.`, 404);
    }

    return interview;
  }

  /**
   * List all interview sessions with filters (status, department, search, sorting)
   */
  static async getInterviews(params: {
    status?: string;
    department?: string;
    search?: string;
    sortBy?: string;
    sortOrder?: 'asc' | 'desc';
    limit?: number;
    offset?: number;
  }) {
    const { status, department, search, sortBy = 'interviewDate', sortOrder = 'desc', limit, offset } = params;

    const where: any = {};

    if (status && status !== 'All' && status.trim() !== '') {
      where.interviewStatus = status.trim().toUpperCase();
    }

    if (department && department !== 'All' && department.trim() !== '') {
      where.student = {
        department: { equals: department.trim(), mode: 'insensitive' },
      };
    }

    if (search && search.trim() !== '') {
      const q = search.trim();
      where.OR = [
        { rollNumber: { contains: q, mode: 'insensitive' } },
        { student: { name: { contains: q, mode: 'insensitive' } } },
      ];
    }

    const interviews = await prisma.interview.findMany({
      where,
      include: {
        student: true,
        stages: {
          orderBy: { stageNumber: 'asc' },
        },
        feedback: true,
      },
      orderBy: {
        [sortBy === 'readinessScore' || sortBy === 'overallScore' ? sortBy : 'interviewDate']: sortOrder,
      },
      ...(limit ? { take: Number(limit) } : {}),
      ...(offset ? { skip: Number(offset) } : {}),
    });

    const totalCount = await prisma.interview.count({ where });

    return {
      total: totalCount,
      interviews,
    };
  }
}
