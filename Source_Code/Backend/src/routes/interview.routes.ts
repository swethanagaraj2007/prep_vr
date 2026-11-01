import { Router } from 'express';
import { z } from 'zod';
import { InterviewController } from '../controllers/interview.controller';
import { validateBody } from '../middleware/validate.middleware';

const router = Router();

const createInterviewSchema = z.object({
  studentId: z.string().optional(),
  rollNumber: z.string().min(1, 'Roll number is required'),
  studentName: z.string().optional(),
  department: z.string().optional(),
  interviewDate: z.string().optional(),
  interviewDuration: z.number().nonnegative().optional(),
  interviewStatus: z.enum(['PENDING', 'IN_PROGRESS', 'COMPLETED', 'FAILED']).optional(),
  overallScore: z.number().min(0).max(100).optional().nullable(),
  readinessScore: z.number().min(0).max(100).optional().nullable(),
  readinessLevel: z.string().optional().nullable(),
  communicationScore: z.number().min(0).max(10).optional().nullable(),
  confidenceScore: z.number().min(0).max(10).optional().nullable(),
  professionalismScore: z.number().min(0).max(10).optional().nullable(),
  behavioralScore: z.number().min(0).max(10).optional().nullable(),
  hiringManagerScore: z.number().min(0).max(10).optional().nullable(),
  feedback: z.any().optional(),
  reportUrl: z.string().optional().nullable(),
  reportPath: z.string().optional().nullable(),
  reportIdentifier: z.string().optional().nullable(),
  stages: z
    .array(
      z.object({
        stageNumber: z.number().int().min(1).max(3),
        stageName: z.string(),
        durationSeconds: z.number().optional(),
        score: z.number().optional().nullable(),
        questionCount: z.number().optional(),
        answerCount: z.number().optional(),
        transcriptSummary: z.string().optional().nullable(),
      })
    )
    .optional(),
  idempotencyKey: z.string().optional(),
});

// POST /api/interviews - Record a completed or updated interview
router.post('/', validateBody(createInterviewSchema), InterviewController.createInterview);

// GET /api/interviews - List all interviews (supports status, dept, search filters)
router.get('/', InterviewController.getInterviews);

// GET /api/interviews/:id - Fetch an interview record
router.get('/:id', InterviewController.getInterviewById);

export default router;
