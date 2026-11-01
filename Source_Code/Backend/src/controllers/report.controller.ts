import { Request, Response, NextFunction } from 'express';
import { ReportService } from '../services/report.service';

export class ReportController {
  static async exportCsv(req: Request, res: Response, next: NextFunction) {
    try {
      const { type = 'rankings' } = req.query;
      const { filename, csv } = await ReportService.generateCsvReport(type as string, req.query);

      res.setHeader('Content-Type', 'text/csv');
      res.setHeader('Content-Disposition', `attachment; filename="${filename}"`);
      return res.status(200).send(csv);
    } catch (error) {
      next(error);
    }
  }
}
