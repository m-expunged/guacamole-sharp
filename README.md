# guacamole-sharp

> ⚠️ Previous working version of guacamole-sharp docker image is now avaible under the "1.0.0" tag. The new version "2.0.0" is still being tested.

guacamole-sharp is a C# replacement of the Apache Guacamole server-side Java servlet.
It is intended for customizable integration of Apache Guacamole into existing frontend projects with their own user and connection management.

Like the official Java guacamole-client **guacamole-common**, guacamole-sharp makes use of the guacamole-protocol and is intended to be used with **guacamole-common-js** (frontend client) and **guacd** (server-side proxy) by Apache. Check out the [Apache Guacamole Docs](https://guacamole.apache.org/doc/gug/) for detailed information.

Inspired by Vadim Pronin's [guacamole-lite](https://github.com/vadimpronin/guacamole-lite).

## Usage

While not strictly required, the intended use of guacamole-sharp is with Docker.

### Quick rundown

- Prepare SSH/RDP/VNC on the target machine
- Run guacd docker container
- Run guacamole-sharp docker container
- Generate connection token through guacamole-sharp endpoint
- Send connection token to guacamole-sharp via guacamole-common-js client
- Display connection in your frontend

![guacamole-sharp diagram](Assets/images/diagram.png?raw=true)

### Docker

guacamole-sharp requires two containers to work:

- guacamole/guacd

```bash
docker run --name guacd
-p 4822:4822
-d guacamole/guacd
```

- [manuelexpunged/guacamolesharp](https://hub.docker.com/r/manuelexpunged/guacamolesharp/tags)

```bash
docker run --name guacamolesharp
--link guacd:guacd
-e GuacamoleSharp:Password=YourTokenEncryptionPasswordHere
-e Guacd:Hostname=guacd
-e Guacd:Port=4822
-p 80:80
-p 8080:8080
-d manuelexpunged/guacamolesharp:2.0.0
```

With the configuration above, the guacamole-sharp container will listen for API calls on port 80 and for WebSocket connections on port 8080.

A docker compose example is included in the repository.

### The connection token

guacamole-sharp transports the arguments for remote connections via an encrypted connection token. This is done to provide a bare minimum of protection against rogue connections without to much hassle for users. Ideally you should build additional authentication/authorization layers on top of the token.

The token is passed to the guacamole-common-js client instance as a parameter of the connect method.

```js
let tunnel = new Guacamole.WebSocketTunnel("ws://localhost:8080");
let client = new Guacamole.Client(tunnel);

client.connect(
  "token=hD12AB5Js4WD0Cse6mtgw_8msAieiSi1-vHajL2vAZgAo24yPufxuLKefeZxEYyWXhcbW21iv53Pv18gTXTnXp1i7wClkQ2tDutnIHqrHRo"
);
```

To generate such a token, guacamole-sharp exposes an endpoint.

```
'HOSTNAME:PORT/TOKEN_ENCRYPTION_PASSWORD'
```

Curl example:

```bash
curl -X 'POST' \
 'http://localhost:5072/YourTokenEncryptionPasswordHere' \
 -H 'accept: text/plain' \
 -H 'Content-Type: application/json' \
 -d '{
"arguments": {
"hostname": "127.0.0.1",
"port": "22"
},
"type": "ssh"
}'
```

Connection object example:

```json
{
  "arguments": {
    "hostname": "127.0.0.1",
    "port": "3389",
    "username": "user",
    "password": "password",
    "security": "any",
    "ignore-cert": "true"
  },
  "type": "rdp"
}
```

The request requires two things:

- The token encryption password as query parameter
- A connection object inside the request body

The token encryption password is defined while creating the Docker container through the environment variables (alternatively inside the appsettings.json if you are building your own image).

The connection object contains all parameters guacd needs to create a connection. **Type** decides the type of connection (ssh, vnc, rdp, ...) and **arguments** is used to configure the connection and pass username and password of the machine you want to connect to. [The Apache Guacamole documentation](https://guacamole.incubator.apache.org/doc/gug/configuring-guacamole.html#connection-configuration) contains a full list of arguments and their meaning.

### guacamole-common-js

In order to use guacamole-sharp, you will need to use the guacamole-common-js library in your frontend. You can find a detailed explanation in the Apache Guacamole Docs.

A good way to get started is to look at the Angular example that is included in the repository, using the guacamole-common-js npm package. You might need to create a d.ts file if you are using Typescript in your project.

Also, pay attention to the z-index, height and width of the guacamole-common-js display element and its parent/child elements, specifically the canvas elements. In some cases the lib creates them with weird values (e.g. z-index: -1) and you might need to change them after the element is added to the DOM.

If you want to include your own examples or notice something that could have been done better feel free to open a issue/pull request.

## Advanced usage

### appsettings.json

In order to simplify the configuration of connections, you can specify default and unencrypted arguments for different connection types inside the appsettings.json and build your own docker image from that.

### Default Guacd, WebSocket server and token arguments

By default, the guacamole-sharp WebSocket server listens on port 8080 and tries to connect to guacd on port 4822. If you want to specify different default ports you can change them in the appsettings.json. Here you can also specify the token password if Docker isn't an option. Make sure the guacd port here is the same as the port of the actual guacd docker image!

```json
"GuacamoleSharp": {
  "Port": 8080,
  "Password": "YourTokenEncryptionPasswordHere"
},
"Guacd": {
  "Hostname": "127.0.0.1",
  "Port": 4822
},
```

**The WebSocket server will always listen on all network interfaces for client activity, only the port can be changed.**

### Default connection arguments

Since many connection arguments will be the same across different connections, it might be convenient to define them as default values.

Any valid guacamole-protocol argument can be used. The most common arguments are already preconfigured and will work out of the box. You might want to change 'width' and 'height' to your preferred resolution.

The properties inside 'DefaultArguments' are generally **lowercase**.

```json
"Client": {
  "DefaultArguments": {
    "rdp": {
      "args": "connect",
      "port": 3389,
      "width": 1024,
      "height": 768,
      "dpi": 96
    },
    "vnc": {
      "args": "connect",
      "port": 5900,
      "width": 1024,
      "height": 768,
      "dpi": 96
    },
    "ssh": {
      "args": "connect",
      "port": 22,
      "width": 1024,
      "height": 768,
      "dpi": 96,
      "server-alive-interval": 10
    },
    "telnet": {
      "args": "connect",
      "port": 23,
      "width": 1024,
      "height": 768,
      "dpi": 96
    }
  }
}
```

### Unencrypted connection arguments

While most arguments for connections should be packed into the encrypted token string, there is also the option to send certain arguments as an unencrypted query parameter by adding them to the unencrypted connection arguments dictionary for their respective connection type.

All valid guacamole-protocol arguments can used in their unencrypted form, except for the connection type (ssh/rdp/...) which is the only argument that is required to be passed via the connection token.

The properties inside 'UnencryptedArguments' are generally **lowercase**.

By default, these arguments can be sent unencrypted:

```json
"UnencryptedArguments": {
  "rdp": ["width", "height", "dpi"],
  "vnc": ["width", "height", "dpi"],
  "ssh": ["color-scheme", "font-name", "font-size", "width", "height", "dpi"],
  "telnet": ["color-scheme", "font-name", "font-size", "width", "height", "dpi"]
}
```

Example unencrypted arguments usage:

```js
let tunnel = new Guacamole.WebSocketTunnel("ws://localhost:8080");
let client = new Guacamole.Client(tunnel);

let connectionString =
  "token=hD12AB5Js4WD0Cse6mtgw_8msAieiSi1-vHajL2vAZgAo24yPufxuLKefeZxEYyWXhcbW21iv53Pv18gTXTnXp1i7wClkQ2tDutnIHqrHRo";
connectionString += "&width=1024";
connectionString += "&height=768";

client.connect(connectionString);
```

### Fallback arguments

Some arguments have fallback values and can be left empty in appsettings.json/Dockerfile:

```c#
GuacamoleSharp:MaxInactivityAllowedInMin = 10
GuacamoleSharp:Port = 8080
Guacd:Hostname = "127.0.0.1"
Guacd:Port = 4822
```

### Arguments overwrite priority

If a guacamole-protocol argument is defined multiple times, it will be overwritten by the argument with the highest priority.
The order of priority is as follows (from highest to lowest):

Unencrypted values > Token string values > Default values > Fallback values
