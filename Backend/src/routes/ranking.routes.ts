import { Router } from 'express';
import { RankingController } from '../controllers/ranking.controller';

const router = Router();

// GET /api/rankings - Candidate rankings sorted by readiness and overall score
router.get('/', RankingController.getRankings);

export default router;
