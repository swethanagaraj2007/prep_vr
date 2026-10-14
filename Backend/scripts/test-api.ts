import http from 'http';

const BASE_URL = 'http://localhost:5000';

function request(
  method: string,
  path: string,
  body?: any,
  token?: string
): Promise<{ status: number; body: any }> {
  return new Promise((resolve, reject) => {
    const url = new URL(path, BASE_URL);
    const dataString = body ? JSON.stringify(body) : '';

    const req = http.request(
      url,
      {
        method,
        headers: {
          'Content-Type': 'application/json',
          ...(dataString ? { 'Content-Length': Buffer.byteLength(dataString) } : {}),
          ...(token ? { Authorization: `Bearer ${token}` } : {}),
        },
      },
      (res) => {
        let raw = '';
        res.on('data', (chunk) => (raw += chunk));
        res.on('end', () => {
          try {
            const parsed = JSON.parse(raw);
            resolve({ status: res.statusCode || 500, body: parsed });
          } catch {
            resolve({ status: res.statusCode || 500, body: raw });
          }
        });
      }
    );

    req.on('error', reject);
    if (dataString) req.write(dataString);
    req.end();
  });
}

async function runTests() {
  console.log('====================================================');
  console.log(' PREP VR - AUTOMATED BACKEND API VERIFICATION SUITE');
  console.log('====================================================\n');

  let passed = 0;
  let failed = 0;

  async function assertTest(name: string, fn: () => Promise<boolean>) {
    process.stdout.write(`• Testing: ${name}... `);
    try {
      const ok = await fn();
      if (ok) {
        console.log('✔ PASSED');
        passed++;
      } else {
        console.log('✖ FAILED');
        failed++;
      }
    } catch (err: any) {
      console.log(`✖ ERROR: ${err.message}`);
      failed++;
    }
  }

  // 1. Health check
  await assertTest('GET /api/health (Service status)', async () => {
    const res = await request('GET', '/api/health');
    return res.status === 200 && res.body.status === 'healthy';
  });

  // 2. Staff Authentication
  let authToken = '';
  await assertTest('POST /api/auth/login (Placement Staff Auth)', async () => {
    const res = await request('POST', '/api/auth/login', {
      email: 'admin@prepvr.edu',
      password: 'Placement@2026',
    });
    if (res.status === 200 && res.body.data?.token) {
      authToken = res.body.data.token;
      return true;
    }
    return false;
  });

  // 3. Verify Staff Profile
  await assertTest('GET /api/auth/me (Protected Route with JWT)', async () => {
    const res = await request('GET', '/api/auth/me', null, authToken);
    return res.status === 200 && res.body.data?.email === 'admin@prepvr.edu';
  });

  // 4. Create Student
  const testRoll = `TST${Math.floor(100 + Math.random() * 900)}`;
  await assertTest(`POST /api/students (Register student: ${testRoll})`, async () => {
    const res = await request('POST', '/api/students', {
      rollNumber: testRoll,
      name: 'Test Student Automated',
      department: 'Computer Science & Engineering',
      year: 4,
      email: `${testRoll.toLowerCase()}@college.edu`,
    });
    return res.status === 201 && res.body.data?.rollNumber === testRoll;
  });

  // 5. List Students
  await assertTest('GET /api/students (List students with filters)', async () => {
    const res = await request('GET', '/api/students');
    return res.status === 200 && Array.isArray(res.body.data) && res.body.data.length > 0;
  });

  // 6. Get Student By ID/Roll
  await assertTest(`GET /api/students/:id (Retrieve ${testRoll})`, async () => {
    const res = await request('GET', `/api/students/${testRoll}`);
    return res.status === 200 && res.body.data?.rollNumber === testRoll;
  });

  // 7. Store Completed Interview (Unity Contract)
  let createdInterviewId = '';
  await assertTest(`POST /api/interviews (Store completed VR interview for ${testRoll})`, async () => {
    const payload = {
      rollNumber: testRoll,
      interviewDate: new Date().toISOString(),
      interviewDuration: 600,
      interviewStatus: 'COMPLETED',
      overallScore: 89.5,
      readinessScore: 93.0,
      communicationScore: 9.0,
      confidenceScore: 8.5,
      professionalismScore: 9.0,
      behavioralScore: 9.0,
      hiringManagerScore: 8.5,
      reportUrl: `/reports/PREP_VR_Placement_Report_${testRoll}.pdf`,
      stages: [
        {
          stageNumber: 1,
          stageName: 'HR Interviewer',
          durationSeconds: 200,
          score: 90,
          questionCount: 4,
          answerCount: 4,
          transcriptSummary: 'Reviewed candidate resume projects and motivations.',
        },
        {
          stageNumber: 2,
          stageName: 'Behavioral Interviewer',
          durationSeconds: 200,
          score: 90,
          questionCount: 4,
          answerCount: 4,
          transcriptSummary: 'Evaluated team collaboration and stress management.',
        },
        {
          stageNumber: 3,
          stageName: 'Hiring Manager',
          durationSeconds: 200,
          score: 85,
          questionCount: 4,
          answerCount: 4,
          transcriptSummary: 'Assessed system architecture and performance trade-offs.',
        },
      ],
      feedback: {
        overallSummary: 'Candidate demonstrated proficient readiness in the 10-minute session.',
        strengths: ['Clear STAR articulation', 'Grounded project understanding'],
        weaknesses: ['Add quantifiable business metrics'],
        recommendations: ['Practice 2-minute elevator pitch'],
        observableEyeContact: 'Maintained steady forward gaze towards interview panel.',
        observablePostureBodyLanguage: 'Upright and centered posture sustained.',
        observableEngagement: 'Response latency averaged 1.9 seconds.',
      },
    };

    const res = await request('POST', '/api/interviews', payload);
    if (res.status === 201 && res.body.data?.id) {
      createdInterviewId = res.body.data.id;
      return true;
    }
    return false;
  });

  // 8. Test Duplicate Protection
  await assertTest('POST /api/interviews (Duplicate Protection check)', async () => {
    const payload = {
      rollNumber: testRoll,
      overallScore: 89.5,
      interviewStatus: 'COMPLETED',
    };
    const res = await request('POST', '/api/interviews', payload);
    // Should return existing without creating another record
    return res.status === 201 && res.body.data?.id === createdInterviewId;
  });

  // 9. Fetch Student Interviews History
  await assertTest(`GET /api/students/:id/interviews (Attempt history for ${testRoll})`, async () => {
    const res = await request('GET', `/api/students/${testRoll}/interviews`);
    return res.status === 200 && Array.isArray(res.body.data) && res.body.data.length >= 1;
  });

  // 10. Dashboard Summary
  await assertTest('GET /api/dashboard/summary (Placement overview stats)', async () => {
    const res = await request('GET', '/api/dashboard/summary');
    const d = res.body.data;
    return (
      res.status === 200 &&
      d.totalStudents >= 1 &&
      d.completedInterviews >= 1 &&
      typeof d.averageScore === 'number' &&
      typeof d.averageReadiness === 'number'
    );
  });

  // 11. Student Rankings
  await assertTest('GET /api/rankings (Candidate rankings ordered by readiness)', async () => {
    const res = await request('GET', '/api/rankings');
    const rankings = res.body.data;
    return (
      res.status === 200 &&
      Array.isArray(rankings) &&
      rankings.length >= 1 &&
      rankings[0].rank === 1 &&
      rankings[0].readinessScore >= (rankings[1]?.readinessScore ?? 0)
    );
  });

  // 12. Clean up Test Record
  await assertTest(`DELETE /api/students/:id (Purge test student ${testRoll})`, async () => {
    const res = await request('DELETE', `/api/students/${testRoll}`);
    return res.status === 200;
  });

  console.log('\n====================================================');
  console.log(` RESULTS: ${passed} PASSED, ${failed} FAILED`);
  console.log('====================================================');

  if (failed > 0) {
    process.exit(1);
  }
}

runTests().catch((err) => {
  console.error('Test suite failed:', err);
  process.exit(1);
});
