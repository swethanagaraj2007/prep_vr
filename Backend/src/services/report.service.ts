import prisma from '../prisma/client';
import { RankingService } from './ranking.service';
import { DashboardService } from './dashboard.service';

export class ReportService {
  /**
   * Generate CSV content for various report types.
   */
  static async generateCsvReport(type: string, queryParams: any): Promise<{ filename: string; csv: string }> {
    const reportType = (type || 'rankings').toLowerCase();
    const timestamp = new Date().toISOString().split('T')[0];

    if (reportType === 'rankings') {
      const rankings = await RankingService.getRankings({
        department: queryParams.department,
        status: queryParams.status,
      });

      const headers = [
        'Rank',
        'Roll Number',
        'Student Name',
        'Department',
        'Academic Year',
        'Interview Status',
        'Readiness Score',
        'Readiness Level',
        'Overall Score',
        'Communication (out of 10)',
        'Confidence (out of 10)',
        'Professionalism (out of 10)',
        'Behavioral (out of 10)',
        'Hiring Manager (out of 10)',
        'Interview Date',
        'Interview Duration (sec)',
      ];

      const rows = rankings.map((r) => [
        r.rank > 0 ? r.rank : 'Unranked',
        `"${r.rollNumber}"`,
        `"${r.studentName}"`,
        `"${r.department}"`,
        r.year,
        r.interviewStatus,
        r.readinessScore ?? 'N/A',
        `"${r.readinessLevel ?? 'N/A'}"`,
        r.overallScore ?? 'N/A',
        r.communicationScore ?? 'N/A',
        r.confidenceScore ?? 'N/A',
        r.professionalismScore ?? 'N/A',
        r.behavioralScore ?? 'N/A',
        r.hiringManagerScore ?? 'N/A',
        r.interviewDate ? new Date(r.interviewDate).toLocaleDateString() : 'N/A',
        r.interviewDuration ?? 0,
      ]);

      const csv = [headers.join(','), ...rows.map((r) => r.join(','))].join('\n');
      return { filename: `prepvr_placement_rankings_${timestamp}.csv`, csv };
    }

    if (reportType === 'students') {
      const students = await prisma.student.findMany({
        include: {
          interviews: {
            orderBy: { interviewDate: 'desc' },
            take: 1,
          },
        },
        orderBy: { rollNumber: 'asc' },
      });

      const headers = [
        'Roll Number',
        'Student Name',
        'Department',
        'Year',
        'College Email',
        'Latest Interview Status',
        'Readiness Score',
        'Overall Score',
        'Latest Interview Date',
      ];

      const rows = students.map((s) => {
        const latest = s.interviews[0];
        return [
          `"${s.rollNumber}"`,
          `"${s.name}"`,
          `"${s.department}"`,
          s.year,
          `"${s.email}"`,
          latest?.interviewStatus ?? 'PENDING',
          latest?.readinessScore ?? 'N/A',
          latest?.overallScore ?? 'N/A',
          latest?.interviewDate ? new Date(latest.interviewDate).toLocaleDateString() : 'N/A',
        ];
      });

      const csv = [headers.join(','), ...rows.map((r) => r.join(','))].join('\n');
      return { filename: `prepvr_students_directory_${timestamp}.csv`, csv };
    }

    if (reportType === 'sessions') {
      const interviews = await prisma.interview.findMany({
        include: { student: true, stages: true },
        orderBy: { interviewDate: 'desc' },
      });

      const headers = [
        'Session ID',
        'Roll Number',
        'Student Name',
        'Department',
        'Date',
        'Duration (Minutes)',
        'Status',
        'Readiness Score',
        'Overall Score',
        'Stages Evaluated',
      ];

      const rows = interviews.map((i) => [
        `"${i.id}"`,
        `"${i.rollNumber}"`,
        `"${i.student.name}"`,
        `"${i.student.department}"`,
        new Date(i.interviewDate).toISOString(),
        Math.round((i.interviewDuration / 60) * 10) / 10,
        i.interviewStatus,
        i.readinessScore ?? 'N/A',
        i.overallScore ?? 'N/A',
        i.stages.length,
      ]);

      const csv = [headers.join(','), ...rows.map((r) => r.join(','))].join('\n');
      return { filename: `prepvr_interview_sessions_${timestamp}.csv`, csv };
    }

    if (reportType === 'department') {
      const summary = await DashboardService.getSummary();
      const headers = [
        'Department',
        'Total Students',
        'Completed Interviews',
        'Completion Rate (%)',
        'Average Score',
        'Average Readiness',
      ];

      const rows = summary.departmentBreakdown.map((d) => [
        `"${d.department}"`,
        d.totalStudents,
        d.completedInterviews,
        d.totalStudents > 0 ? Math.round((d.completedInterviews / d.totalStudents) * 100) : 0,
        d.averageScore,
        d.averageReadiness,
      ]);

      const csv = [headers.join(','), ...rows.map((r) => r.join(','))].join('\n');
      return { filename: `prepvr_department_performance_${timestamp}.csv`, csv };
    }

    throw new Error(`Unsupported report type: ${type}`);
  }
}
