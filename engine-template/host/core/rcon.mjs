import net from 'node:net';
import { createHash } from 'node:crypto';

const MAX_PACKET = 1024 * 1024;

export function encodePacket(sequence, words) {
  const encoded = words.map(word => Buffer.from(String(word), 'utf8'));
  const size = 12 + encoded.reduce((sum, word) => sum + 5 + word.length, 0);
  if (size > MAX_PACKET) throw new Error('Packet too large');
  const packet = Buffer.alloc(size);
  packet.writeUInt32LE(sequence >>> 0, 0);
  packet.writeUInt32LE(size, 4);
  packet.writeUInt32LE(words.length, 8);
  let offset = 12;
  for (const word of encoded) {
    packet.writeUInt32LE(word.length, offset);
    word.copy(packet, offset + 4);
    offset += word.length + 5;
  }
  return packet;
}

export function decodePacket(packet) {
  if (packet.length < 12 || packet.length > MAX_PACKET) throw new Error('Invalid packet size');
  if (packet.readUInt32LE(4) !== packet.length) throw new Error('Length mismatch');
  const flags = packet.readUInt32LE(0);
  const count = packet.readUInt32LE(8);
  if (count > (packet.length - 12) / 5) throw new Error('Invalid word count');
  let offset = 12;
  const words = [];
  for (let index = 0; index < count; index++) {
    if (offset + 4 > packet.length) throw new Error('Truncated length');
    const length = packet.readUInt32LE(offset);
    offset += 4;
    if (offset + length >= packet.length) throw new Error('Truncated word');
    if (packet[offset + length] !== 0) throw new Error('Missing word terminator');
    words.push(packet.toString('utf8', offset, offset + length));
    offset += length + 1;
  }
  if (offset !== packet.length) throw new Error('Trailing data');
  return { sequence: flags & 0x3fffffff, response: Boolean(flags & 0x40000000), serverOrigin: Boolean(flags & 0x80000000), words };
}

export class Rcon {
  constructor(socket) {
    this.socket = socket;
    this.sequence = 0;
    this.buffer = Buffer.alloc(0);
    this.pending = new Map();
    socket.on('data', chunk => {
      try {
        this.buffer = Buffer.concat([this.buffer, chunk]);
        while (this.buffer.length >= 12) {
          const length = this.buffer.readUInt32LE(4);
          if (length < 12 || length > MAX_PACKET) throw new Error('Invalid frame length');
          if (this.buffer.length < length) break;
          const packet = decodePacket(this.buffer.subarray(0, length));
          this.buffer = this.buffer.subarray(length);
          const request = this.pending.get(packet.sequence);
          if (packet.response && !packet.serverOrigin && request) {
            clearTimeout(request.timer);
            this.pending.delete(packet.sequence);
            request.resolve(packet.words);
          }
        }
      } catch (error) { this.fail(error); socket.destroy(); }
    });
    socket.on('error', error => this.fail(error));
    socket.on('close', () => this.fail(new Error('RCON disconnected')));
  }
  static async connect(host, port, timeout = 4000) {
    return new Promise((resolve, reject) => {
      const socket = net.createConnection({ host, port });
      const timer = setTimeout(() => socket.destroy(new Error('RCON connect timeout')), timeout);
      const fail = error => { clearTimeout(timer); reject(error); };
      socket.once('error', fail);
      socket.once('connect', () => {
        clearTimeout(timer);
        socket.removeListener('error', fail);
        resolve(new Rcon(socket));
      });
    });
  }
  fail(error) {
    for (const request of this.pending.values()) { clearTimeout(request.timer); request.reject(error); }
    this.pending.clear();
  }
  command(...words) {
    if (this.socket.destroyed) return Promise.reject(new Error('RCON socket closed'));
    const sequence = this.sequence++ & 0x3fffffff;
    return new Promise((resolve, reject) => {
      const timer = setTimeout(() => { this.pending.delete(sequence); reject(new Error(`RCON timeout: ${words[0]}`)); }, 4000);
      this.pending.set(sequence, { resolve, reject, timer });
      this.socket.write(encodePacket(sequence, words), error => {
        if (error) { clearTimeout(timer); this.pending.delete(sequence); reject(error); }
      });
    });
  }
  async login(password) {
    const challenge = await this.command('login.hashed');
    if (challenge[0] !== 'OK' || !/^[a-f0-9]{32}$/i.test(challenge[1] ?? '')) throw new Error('Invalid login challenge');
    const digest = createHash('md5').update(Buffer.from(challenge[1], 'hex')).update(password, 'utf8').digest('hex').toUpperCase();
    const response = await this.command('login.hashed', digest);
    if (response[0] !== 'OK') throw new Error(`RCON authentication rejected: ${response[0]}`);
  }
  close() { this.socket.destroy(); }
}
