const net = require('net');
const { execSync } = require('child_process');

function getWslIp() {
  try {
    const out = execSync('wsl -d Ubuntu hostname -I').toString().trim();
    return out.split(' ')[0] || '127.0.0.1';
  } catch {
    return '127.0.0.1';
  }
}

const wslIp = getWslIp();
console.log(`[Windows-Port-Bridge] Forwarding Windows 127.0.0.1:5000 -> WSL ${wslIp}:5000`);

const server = net.createServer((clientSocket) => {
  const targetSocket = net.createConnection(5000, wslIp);
  clientSocket.pipe(targetSocket).pipe(clientSocket);

  clientSocket.on('error', () => {});
  targetSocket.on('error', () => {});
});

server.listen(5000, '0.0.0.0', () => {
  console.log(`[Windows-Port-Bridge] Active on http://localhost:5000 and http://127.0.0.1:5000`);
});
