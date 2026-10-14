import { Router } from 'express';
import { ReportController } from '../controllers/report.controller';

const router = Router();

// GET /api/reports/export-csv - Download CSV report
router.get('/export-csv', ReportController.exportCsv);

export default router;
