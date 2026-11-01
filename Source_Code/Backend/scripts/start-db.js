/**
 * PREP VR - Local PostgreSQL Connection Bridge
 * Forwards Windows localhost:5432 to WSL PostgreSQL 18
 */

const net = require('net');

const LOCAL_PORT = 5432;
const TARGET_HOST = '172.26.136.130';
const TARGET_PORT = 5432;

function startProxy() {
  const server = net.createServer((clientSocket) => {
    const targetSocket = net.createConnection(TARGET_PORT, TARGET_HOST);

    clientSocket.pipe(targetSocket);
    targetSocket.pipe(clientSocket);

    clientSocket.on('error', (err) => {
      // console.error('[Proxy Client Error]', err.message);
    });
    targetSocket.on('error', (err) => {
      // console.error('[Proxy Target Error]', err.message);
    });
  });

  server.on('error', (err) => {
    if (err.code === 'EADDRINUSE') {
      console.log(`[PG-Proxy] Port ${LOCAL_PORT} already in use. Assuming Postgres or proxy is active.`);
    } else {
      console.error('[PG-Proxy Error]', err.message);
    }
  });

  server.listen(LOCAL_PORT, '127.0.0.1', () => {
    console.log(`[PG-Proxy] Listening on 127.0.0.1:${LOCAL_PORT} -> forwarding to ${TARGET_HOST}:${TARGET_PORT}`);
  });
}

startProxy();
