import { Router } from 'express';
import { DashboardController } from '../controllers/dashboard.controller';

const router = Router();

// GET /api/dashboard/summary - Placement overview stats
router.get('/summary', DashboardController.getSummary);

export default router;
