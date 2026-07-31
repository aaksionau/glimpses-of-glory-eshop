const fs = require('fs');

fs.mkdirSync('./wwwroot/js', { recursive: true });
fs.copyFileSync('./node_modules/alpinejs/dist/cdn.min.js', './wwwroot/js/alpine.min.js');
