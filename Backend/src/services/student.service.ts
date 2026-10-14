import prisma from '../prisma/client';
import { CreateStudentInput } from '../types';
import { AppError } from '../middleware/errorHandler.middleware';

export class StudentService {
  /**
   * Register a new student. Roll number and email must be unique.
   */
  static async createStudent(data: CreateStudentInput) {
    const rollNumberClean = data.rollNumber.trim().toUpperCase();
    const emailClean = data.email.trim().toLowerCase();

    // Check if roll number already exists
    const existingRoll = await prisma.student.findUnique({
      where: { rollNumber: rollNumberClean },
    });
    if (existingRoll) {
      throw new AppError(`Student with roll number '${rollNumberClean}' already exists.`, 409);
    }

    // Check if email already exists
    const existingEmail = await prisma.student.findUnique({
      where: { email: emailClean },
    });
    if (existingEmail) {
      throw new AppError(`Student with email '${emailClean}' already exists.`, 409);
    }

    const student = await prisma.student.create({
      data: {
        rollNumber: rollNumberClean,
        name: data.name.trim(),
        department: data.department.trim(),
        year: data.year ? Number(data.year) : 4,
        email: emailClean,
      },
    });

    return student;
  }

  /**
   * Fetch all students with optional search, filtering and sorting.
   */
  static async getStudents(params: {
    search?: string;
    department?: string;
    status?: string;
    sortBy?: string;
    sortOrder?: 'asc' | 'desc';
  }) {
    const { search, department, status, sortBy = 'createdAt', sortOrder = 'desc' } = params;

    const where: any = {};

    if (search && search.trim() !== '') {
      const q = search.trim();
      where.OR = [
        { name: { contains: q, mode: 'insensitive' } },
        { rollNumber: { contains: q, mode: 'insensitive' } },
        { email: { contains: q, mode: 'insensitive' } },
      ];
    }

    if (department && department !== 'All' && department.trim() !== '') {
      where.department = { equals: department.trim(), mode: 'insensitive' };
    }

    // If filtering by interview status
    if (status && status !== 'All' && status.trim() !== '') {
      const statusUpper = status.trim().toUpperCase();
      if (statusUpper === 'PENDING') {
        where.OR = [
          { interviews: { none: {} } },
          { interviews: { some: { interviewStatus: 'PENDING' } } },
        ];
      } else {
        where.interviews = {
          some: {
            interviewStatus: statusUpper,
          },
        };
      }
    }

    const students = await prisma.student.findMany({
      where,
      include: {
        interviews: {
          orderBy: { interviewDate: 'desc' },
          take: 1, // Get latest interview for directory listing
          include: {
            feedback: true,
          },
        },
      },
      orderBy: {
        [sortBy === 'name' || sortBy === 'rollNumber' ? sortBy : 'createdAt']: sortOrder,
      },
    });

    return students.map((s) => {
      const latestInterview = s.interviews[0] || null;
      return {
        id: s.id,
        rollNumber: s.rollNumber,
        name: s.name,
        department: s.department,
        year: s.year,
        email: s.email,
        createdAt: s.createdAt,
        updatedAt: s.updatedAt,
        latestInterview: latestInterview
          ? {
              id: latestInterview.id,
              interviewDate: latestInterview.interviewDate,
              interviewDuration: latestInterview.interviewDuration,
              interviewStatus: latestInterview.interviewStatus,
              overallScore: latestInterview.overallScore,
              readinessScore: latestInterview.readinessScore,
              readinessLevel: latestInterview.readinessLevel,
              communicationScore: latestInterview.communicationScore,
              confidenceScore: latestInterview.confidenceScore,
              professionalismScore: latestInterview.professionalismScore,
              behavioralScore: latestInterview.behavioralScore,
              hiringManagerScore: latestInterview.hiringManagerScore,
              reportUrl: latestInterview.reportUrl,
              hasFeedback: !!latestInterview.feedback,
            }
          : null,
      };
    });
  }

  /**
   * Get single student by ID or Roll Number, with complete interview history and feedbacks.
   */
  static async getStudentByIdOrRoll(identifier: string) {
    const student = await prisma.student.findFirst({
      where: {
        OR: [{ id: identifier }, { rollNumber: identifier.toUpperCase() }],
      },
      include: {
        interviews: {
          orderBy: { interviewDate: 'desc' },
          include: {
            stages: {
              orderBy: { stageNumber: 'asc' },
            },
            feedback: true,
          },
        },
      },
    });

    if (!student) {
      throw new AppError(`Student '${identifier}' not found.`, 404);
    }

    return student;
  }

  /**
   * Get all interview attempts for a specific student.
   */
  static async getStudentInterviews(studentIdOrRoll: string) {
    const student = await this.getStudentByIdOrRoll(studentIdOrRoll);
    return student.interviews;
  }

  /**
   * Update student details
   */
  static async updateStudent(id: string, data: Partial<CreateStudentInput>) {
    const existing = await prisma.student.findFirst({
      where: {
        OR: [{ id }, { rollNumber: id.toUpperCase() }],
      },
    });

    if (!existing) {
      throw new AppError(`Student '${id}' not found.`, 404);
    }

    const rollNumberClean = data.rollNumber ? data.rollNumber.trim().toUpperCase() : undefined;
    const emailClean = data.email ? data.email.trim().toLowerCase() : undefined;

    if (rollNumberClean && rollNumberClean !== existing.rollNumber) {
      const conflict = await prisma.student.findUnique({
        where: { rollNumber: rollNumberClean },
      });
      if (conflict) {
        throw new AppError(`Roll number '${rollNumberClean}' is already assigned to another student.`, 409);
      }
    }

    if (emailClean && emailClean !== existing.email) {
      const conflict = await prisma.student.findUnique({
        where: { email: emailClean },
      });
      if (conflict) {
        throw new AppError(`Email '${emailClean}' is already in use by another student.`, 409);
      }
    }

    const updated = await prisma.student.update({
      where: { id: existing.id },
      data: {
        ...(data.name ? { name: data.name.trim() } : {}),
        ...(data.department ? { department: data.department.trim() } : {}),
        ...(rollNumberClean ? { rollNumber: rollNumberClean } : {}),
        ...(emailClean ? { email: emailClean } : {}),
        ...(data.year ? { year: Number(data.year) } : {}),
      },
    });

    return updated;
  }

  /**
   * Delete student and associated interview records
   */
  static async deleteStudent(id: string) {
    const existing = await prisma.student.findFirst({
      where: {
        OR: [{ id }, { rollNumber: id.toUpperCase() }],
      },
    });

    if (!existing) {
      throw new AppError(`Student '${id}' not found.`, 404);
    }

    const interviews = await prisma.interview.findMany({
      where: { studentId: existing.id },
      select: { id: true },
    });
    const interviewIds = interviews.map((i) => i.id);

    if (interviewIds.length > 0) {
      await prisma.feedback.deleteMany({ where: { interviewId: { in: interviewIds } } });
      await prisma.interviewStage.deleteMany({ where: { interviewId: { in: interviewIds } } });
      await prisma.interview.deleteMany({ where: { id: { in: interviewIds } } });
    }

    await prisma.student.delete({ where: { id: existing.id } });

    return { success: true, message: `Student '${existing.rollNumber}' and all interview records removed.` };
  }

  /**
   * Import students in bulk from parsed CSV rows or raw CSV string with validation & duplicate detection.
   */
  static async importCsv(input: {
    csvData?: string;
    students?: Array<{ rollNumber: string; name: string; department?: string; year?: number; email?: string }>;
  }) {
    let rowsToProcess: Array<{ rollNumber: string; name: string; department: string; year: number; email: string }> = [];

    if (input.students && Array.isArray(input.students)) {
      rowsToProcess = input.students.map((s) => ({
        rollNumber: (s.rollNumber || '').trim().toUpperCase(),
        name: (s.name || '').trim(),
        department: (s.department || 'Computer Science & Engineering').trim(),
        year: s.year ? Number(s.year) : 4,
        email: (s.email || `${(s.rollNumber || '').trim().toLowerCase()}@college.edu`).trim().toLowerCase(),
      }));
    } else if (input.csvData && typeof input.csvData === 'string') {
      const lines = input.csvData
        .split(/\r?\n/)
        .map((l) => l.trim())
        .filter((l) => l.length > 0);

      if (lines.length < 2) {
        throw new AppError('CSV must contain a header line and at least one student record.', 400);
      }

      const headers = lines[0].split(',').map((h) => h.trim().toLowerCase().replace(/[\s_-]+/g, ''));
      const rollIdx = headers.findIndex((h) => h.includes('roll') || h.includes('register') || h.includes('id'));
      const nameIdx = headers.findIndex((h) => h.includes('name'));
      const deptIdx = headers.findIndex((h) => h.includes('dept') || h.includes('department'));
      const yearIdx = headers.findIndex((h) => h.includes('year') || h.includes('batch'));
      const emailIdx = headers.findIndex((h) => h.includes('email') || h.includes('mail'));

      if (rollIdx === -1 || nameIdx === -1) {
        throw new AppError('CSV header must contain columns for Roll Number and Name.', 400);
      }

      for (let i = 1; i < lines.length; i++) {
        const parts = lines[i].split(',').map((p) => p.trim());
        const roll = (parts[rollIdx] || '').toUpperCase();
        const name = parts[nameIdx] || '';
        const dept = deptIdx !== -1 && parts[deptIdx] ? parts[deptIdx] : 'Computer Science & Engineering';
        const year = yearIdx !== -1 && parts[yearIdx] ? parseInt(parts[yearIdx], 10) || 4 : 4;
        const email = emailIdx !== -1 && parts[emailIdx] ? parts[emailIdx].toLowerCase() : `${roll.toLowerCase()}@college.edu`;

        if (roll && name) {
          rowsToProcess.push({ rollNumber: roll, name, department: dept, year, email });
        }
      }
    } else {
      throw new AppError('Either csvData string or students array must be provided.', 400);
    }

    if (rowsToProcess.length === 0) {
      throw new AppError('No valid student rows found to import.', 400);
    }

    const inserted: any[] = [];
    const duplicates: string[] = [];
    const errors: string[] = [];
    const seenInBatch = new Set<string>();

    for (const row of rowsToProcess) {
      if (!row.rollNumber || !row.name) {
        errors.push(`Row missing rollNumber or name`);
        continue;
      }

      if (seenInBatch.has(row.rollNumber)) {
        duplicates.push(row.rollNumber);
        continue;
      }
      seenInBatch.add(row.rollNumber);

      const exists = await prisma.student.findFirst({
        where: {
          OR: [{ rollNumber: row.rollNumber }, { email: row.email }],
        },
      });

      if (exists) {
        duplicates.push(row.rollNumber);
        continue;
      }

      try {
        const student = await prisma.student.create({
          data: {
            rollNumber: row.rollNumber,
            name: row.name,
            department: row.department,
            year: row.year,
            email: row.email,
          },
        });
        inserted.push(student);
      } catch (err: any) {
        errors.push(`Failed to import ${row.rollNumber}: ${err.message}`);
      }
    }

    return {
      totalProcessed: rowsToProcess.length,
      importedCount: inserted.length,
      skippedDuplicates: duplicates.length,
      duplicateRollNumbers: duplicates,
      errors,
      students: inserted,
    };
  }
}

