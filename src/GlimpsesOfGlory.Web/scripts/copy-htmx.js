const fs = require('fs');

fs.mkdirSync('./wwwroot/js', { recursive: true });
fs.copyFileSync('./node_modules/htmx.org/dist/htmx.min.js', './wwwroot/js/htmx.min.js');
