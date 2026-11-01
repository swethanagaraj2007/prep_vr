import prisma from '../prisma/client';
import { DashboardSummaryResponse } from '../types';

export class DashboardService {
  /**
   * Aggregate placement overview metrics across the entire student body.
   */
  static async getSummary(): Promise<DashboardSummaryResponse> {
    const totalStudents = await prisma.student.count();

    // Latest interview for each student or all completed interviews
    const allStudents = await prisma.student.findMany({
      include: {
        interviews: {
          orderBy: { interviewDate: 'desc' },
          take: 1,
        },
      },
    });

    let completedCount = 0;
    let inProgressCount = 0;
    let failedCount = 0;
    let pendingCount = 0;

    let totalScoreSum = 0;
    let totalScoreCount = 0;

    let totalReadinessSum = 0;
    let totalReadinessCount = 0;

    let highestScore = 0;

    // Track department stats
    const deptStatsMap: Record<
      string,
      { totalStudents: number; completed: number; scoreSum: number; scoreCount: number; readinessSum: number; readinessCount: number }
    > = {};

    for (const student of allStudents) {
      const dept = student.department || 'General';
      if (!deptStatsMap[dept]) {
        deptStatsMap[dept] = { totalStudents: 0, completed: 0, scoreSum: 0, scoreCount: 0, readinessSum: 0, readinessCount: 0 };
      }
      deptStatsMap[dept].totalStudents++;

      const latestInterview = student.interviews[0];
      if (!latestInterview || latestInterview.interviewStatus === 'PENDING') {
        pendingCount++;
      } else if (latestInterview.interviewStatus === 'IN_PROGRESS') {
        inProgressCount++;
      } else if (latestInterview.interviewStatus === 'FAILED') {
        failedCount++;
      } else if (latestInterview.interviewStatus === 'COMPLETED') {
        completedCount++;
        deptStatsMap[dept].completed++;

        if (latestInterview.overallScore != null) {
          totalScoreSum += latestInterview.overallScore;
          totalScoreCount++;
          deptStatsMap[dept].scoreSum += latestInterview.overallScore;
          deptStatsMap[dept].scoreCount++;
          if (latestInterview.overallScore > highestScore) {
            highestScore = latestInterview.overallScore;
          }
        }

        if (latestInterview.readinessScore != null) {
          totalReadinessSum += latestInterview.readinessScore;
          totalReadinessCount++;
          deptStatsMap[dept].readinessSum += latestInterview.readinessScore;
          deptStatsMap[dept].readinessCount++;
        }
      }
    }

    const averageScore = totalScoreCount > 0 ? Math.round((totalScoreSum / totalScoreCount) * 10) / 10 : 0;
    const averageReadiness = totalReadinessCount > 0 ? Math.round((totalReadinessSum / totalReadinessCount) * 10) / 10 : 0;

    const departmentBreakdown = Object.entries(deptStatsMap).map(([deptName, stats]) => ({
      department: deptName,
      totalStudents: stats.totalStudents,
      completedInterviews: stats.completed,
      averageScore: stats.scoreCount > 0 ? Math.round((stats.scoreSum / stats.scoreCount) * 10) / 10 : 0,
      averageReadiness: stats.readinessCount > 0 ? Math.round((stats.readinessSum / stats.readinessCount) * 10) / 10 : 0,
    }));

    return {
      totalStudents,
      completedInterviews: completedCount,
      pendingInterviews: pendingCount,
      inProgressInterviews: inProgressCount,
      failedInterviews: failedCount,
      averageScore,
      averageReadiness,
      highestScore,
      departmentBreakdown,
    };
  }
}
