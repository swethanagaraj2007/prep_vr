import prisma from '../prisma/client';
import { RankingItem } from '../types';

export class RankingService {
  /**
   * Get students ranked by readiness score (descending) and overall score.
   * Can be filtered by department and status.
   */
  static async getRankings(params: {
    department?: string;
    status?: string;
    limit?: number;
  }): Promise<RankingItem[]> {
    const { department, status, limit } = params;

    const studentWhere: any = {};
    if (department && department !== 'All' && department.trim() !== '') {
      studentWhere.department = { equals: department.trim(), mode: 'insensitive' };
    }

    // Query students with their interviews
    const students = await prisma.student.findMany({
      where: studentWhere,
      include: {
        interviews: {
          orderBy: [
            { readinessScore: 'desc' },
            { overallScore: 'desc' },
            { interviewDate: 'desc' },
          ],
          take: 1, // Top performance attempt per student
          include: {
            feedback: true,
          },
        },
      },
    });

    // Map each student to a ranking candidate
    let list: RankingItem[] = students.map((s) => {
      const topInterview = s.interviews[0] || null;
      return {
        rank: 0,
        studentId: s.id,
        rollNumber: s.rollNumber,
        studentName: s.name,
        department: s.department,
        year: s.year,
        interviewId: topInterview?.id,
        interviewStatus: (topInterview?.interviewStatus as any) || 'PENDING',
        overallScore: topInterview?.overallScore ?? null,
        readinessScore: topInterview?.readinessScore ?? null,
        readinessLevel: topInterview?.readinessLevel ?? null,
        communicationScore: topInterview?.communicationScore ?? null,
        confidenceScore: topInterview?.confidenceScore ?? null,
        professionalismScore: topInterview?.professionalismScore ?? null,
        behavioralScore: topInterview?.behavioralScore ?? null,
        hiringManagerScore: topInterview?.hiringManagerScore ?? null,
        interviewDate: topInterview?.interviewDate ? topInterview.interviewDate.toISOString() : null,
        interviewDuration: topInterview?.interviewDuration ?? null,
        hasReport: !!(topInterview?.reportUrl || topInterview?.reportPath || topInterview?.feedback),
      };
    });

    // Filter by status if specified
    if (status && status !== 'All' && status.trim() !== '') {
      const statusUpper = status.trim().toUpperCase();
      list = list.filter((item) => item.interviewStatus === statusUpper);
    }

    // Sort: COMPLETED / high readiness first, then overallScore, then pending at the bottom
    list.sort((a, b) => {
      const scoreA = a.readinessScore ?? -1;
      const scoreB = b.readinessScore ?? -1;
      if (scoreB !== scoreA) {
        return scoreB - scoreA;
      }
      const overallA = a.overallScore ?? -1;
      const overallB = b.overallScore ?? -1;
      if (overallB !== overallA) {
        return overallB - overallA;
      }
      // Recent interview date tie breaker
      if (a.interviewDate && b.interviewDate) {
        return new Date(b.interviewDate).getTime() - new Date(a.interviewDate).getTime();
      }
      return a.rollNumber.localeCompare(b.rollNumber);
    });

    // Assign rank numbers
    let currentRank = 1;
    for (let i = 0; i < list.length; i++) {
      if (list[i].interviewStatus === 'COMPLETED' && list[i].readinessScore != null) {
        list[i].rank = currentRank++;
      } else {
        list[i].rank = 0; // Unranked / Pending
      }
    }

    if (limit && limit > 0) {
      list = list.slice(0, limit);
    }

    return list;
  }
}
