import { Request, Response, NextFunction } from 'express';
import { RankingService } from '../services/ranking.service';

export class RankingController {
  static async getRankings(req: Request, res: Response, next: NextFunction) {
    try {
      const { department, status, limit } = req.query;
      const rankings = await RankingService.getRankings({
        department: department as string,
        status: status as string,
        limit: limit ? parseInt(limit as string, 10) : undefined,
      });
      return res.status(200).json({
        success: true,
        count: rankings.length,
        data: rankings,
      });
    } catch (error) {
      next(error);
    }
  }
}
