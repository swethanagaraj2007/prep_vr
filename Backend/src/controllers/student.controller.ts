import { Request, Response, NextFunction } from 'express';
import { StudentService } from '../services/student.service';

export class StudentController {
  static async createStudent(req: Request, res: Response, next: NextFunction) {
    try {
      const student = await StudentService.createStudent(req.body);
      return res.status(201).json({
        success: true,
        message: 'Student registered successfully',
        data: student,
      });
    } catch (error) {
      next(error);
    }
  }

  static async getStudents(req: Request, res: Response, next: NextFunction) {
    try {
      const { search, department, status, sortBy, sortOrder } = req.query;
      const students = await StudentService.getStudents({
        search: search as string,
        department: department as string,
        status: status as string,
        sortBy: sortBy as string,
        sortOrder: sortOrder as 'asc' | 'desc',
      });
      return res.status(200).json({
        success: true,
        count: students.length,
        data: students,
      });
    } catch (error) {
      next(error);
    }
  }

  static async getStudentById(req: Request, res: Response, next: NextFunction) {
    try {
      const { id } = req.params;
      const student = await StudentService.getStudentByIdOrRoll(id);
      return res.status(200).json({
        success: true,
        data: student,
      });
    } catch (error) {
      next(error);
    }
  }

  static async getStudentInterviews(req: Request, res: Response, next: NextFunction) {
    try {
      const { id } = req.params;
      const interviews = await StudentService.getStudentInterviews(id);
      return res.status(200).json({
        success: true,
        count: interviews.length,
        data: interviews,
      });
    } catch (error) {
      next(error);
    }
  }

  static async updateStudent(req: Request, res: Response, next: NextFunction) {
    try {
      const { id } = req.params;
      const student = await StudentService.updateStudent(id, req.body);
      return res.status(200).json({
        success: true,
        message: 'Student updated successfully',
        data: student,
      });
    } catch (error) {
      next(error);
    }
  }

  static async deleteStudent(req: Request, res: Response, next: NextFunction) {
    try {
      const { id } = req.params;
      const result = await StudentService.deleteStudent(id);
      return res.status(200).json(result);
    } catch (error) {
      next(error);
    }
  }

  static async importCsv(req: Request, res: Response, next: NextFunction) {
    try {
      const result = await StudentService.importCsv(req.body);
      return res.status(200).json({
        success: true,
        message: `Import completed: ${result.importedCount} added, ${result.skippedDuplicates} duplicates skipped.`,
        data: result,
      });
    } catch (error) {
      next(error);
    }
  }
}

