import { Request, Response, NextFunction } from 'express';
import { InterviewService } from '../services/interview.service';

export class InterviewController {
  static async createInterview(req: Request, res: Response, next: NextFunction) {
    try {
      const interview = await InterviewService.createInterview(req.body);
      return res.status(201).json({
        success: true,
        message: 'Interview record saved successfully',
        data: interview,
      });
    } catch (error) {
      next(error);
    }
  }

  static async getInterviewById(req: Request, res: Response, next: NextFunction) {
    try {
      const { id } = req.params;
      const interview = await InterviewService.getInterviewById(id);
      return res.status(200).json({
        success: true,
        data: interview,
      });
    } catch (error) {
      next(error);
    }
  }

  static async getInterviews(req: Request, res: Response, next: NextFunction) {
    try {
      const { status, department, search, sortBy, sortOrder, limit, offset } = req.query;
      const result = await InterviewService.getInterviews({
        status: status as string,
        department: department as string,
        search: search as string,
        sortBy: sortBy as string,
        sortOrder: sortOrder as 'asc' | 'desc',
        limit: limit ? parseInt(limit as string, 10) : undefined,
        offset: offset ? parseInt(offset as string, 10) : undefined,
      });
      return res.status(200).json({
        success: true,
        data: result.interviews,
        total: result.total,
      });
    } catch (error) {
      next(error);
    }
  }
}
