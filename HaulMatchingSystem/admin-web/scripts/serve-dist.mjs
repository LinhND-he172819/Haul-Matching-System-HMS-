import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';

const host = '127.0.0.1';
const port = 5173;
const root = path.resolve('dist');
const types = {
    '.css': 'text/css',
    '.html': 'text/html',
    '.js': 'text/javascript',
    '.json': 'application/json',
    '.png': 'image/png',
    '.svg': 'image/svg+xml'
};

const server = http.createServer((request, response) => {
    let requestPath = decodeURI((request.url ?? '/').split('?')[0]);
    if (requestPath === '/') {
        requestPath = '/index.html';
    }

    const filePath = path.join(root, requestPath);
    if (!filePath.startsWith(root)) {
        response.writeHead(403);
        response.end('Forbidden');
        return;
    }

    fs.readFile(filePath, (error, body) => {
        if (error) {
            fs.readFile(path.join(root, 'index.html'), (fallbackError, fallbackBody) => {
                if (fallbackError) {
                    response.writeHead(404);
                    response.end('Not found');
                    return;
                }

                response.writeHead(200, { 'Content-Type': 'text/html' });
                response.end(fallbackBody);
            });
            return;
        }

        response.writeHead(200, {
            'Content-Type': types[path.extname(filePath)] ?? 'application/octet-stream'
        });
        response.end(body);
    });
});

server.listen(port, host);
