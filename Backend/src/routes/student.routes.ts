import { Router } from 'express';
import { z } from 'zod';
import { StudentController } from '../controllers/student.controller';
import { validateBody } from '../middleware/validate.middleware';

const router = Router();

const createStudentSchema = z.object({
  rollNumber: z.string().min(1, 'Roll number is required'),
  name: z.string().min(1, 'Student name is required'),
  department: z.string().min(1, 'Department is required'),
  year: z.number().int().min(1).max(5).optional(),
  email: z.string().email('Valid email is required'),
});

// POST /api/students/import-csv - Bulk CSV import with duplicate validation
router.post('/import-csv', StudentController.importCsv);

// POST /api/students - Register a student
router.post('/', validateBody(createStudentSchema), StudentController.createStudent);

// GET /api/students - List all students (supports filter, search, sort)
router.get('/', StudentController.getStudents);

// GET /api/students/:id - Get one student by ID or Roll Number
router.get('/:id', StudentController.getStudentById);

// PUT /api/students/:id - Update student information
router.put('/:id', StudentController.updateStudent);

// DELETE /api/students/:id - Remove student
router.delete('/:id', StudentController.deleteStudent);

// GET /api/students/:id/interviews - Get all interviews for that student
router.get('/:id/interviews', StudentController.getStudentInterviews);

export default router;
