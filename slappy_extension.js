onNotify = function(app, title, body) {
	Log.print(`onNotify(app=${app} title=${title} body=${body})`);
	if(app.match(/thunderbird/i)) {
		var seg = body.split(":");
		if(seg.length >= 2) {
			var sender = seg[0];
			if(sender.endsWith(" より")) {
				sender = sender.slice(0,-3);
			}
			body = seg.filter(n => n!=0).join();
			return new NotificationEvent("thunderbird","[Mail]",sender,body);
		}
	}
	return null;
}

onForeground = function(processName,title) {
	Log.print(`onForeground(processName=${processName} title=${title})`);
	if(processName.match(/thunderbird/i)) {
		return new ViewChangeEvent("thunderbird", "[Mail]", "");
	}
	return null;
}

onTitleChange = function(processName,title) {
	Log.print(`onTitleChange(processName=${processName} title=${title})`);
	return null;
}
