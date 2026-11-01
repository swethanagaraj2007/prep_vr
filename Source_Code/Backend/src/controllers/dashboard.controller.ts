import { Request, Response, NextFunction } from 'express';
import { DashboardService } from '../services/dashboard.service';

export class DashboardController {
  static async getSummary(req: Request, res: Response, next: NextFunction) {
    try {
      const summary = await DashboardService.getSummary();
      return res.status(200).json({
        success: true,
        data: summary,
      });
    } catch (error) {
      next(error);
    }
  }
}
