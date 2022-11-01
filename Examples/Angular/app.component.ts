import { AfterViewInit, Component } from "@angular/core";
// Declare module 'guacamole-common-js' in d.ts file to get around missing typings
import Guacamole from "guacamole-common-js";

@Component({
  selector: "app-root",
  templateUrl: "./app.component.html",
  styleUrls: ["./app.component.css"],
})
export class AppComponent implements AfterViewInit {
  @ViewChild("display")
  displayContainer!: ElementRef;

  ngAfterViewInit(): void {
    // Init tunnel and client
    let tunnel = new Guacamole.WebSocketTunnel("ws://localhost:8080/connect");
    let client = new Guacamole.Client(tunnel);

    // Error handler
    client.onerror = (error: any) => {
      console.log(error);
    };

    // Get guacamole-common-js display and append it to DOM
    let display = client.getDisplay().getElement();
    this.displayContainer.nativeElement.appendChild(display);

    // Mouse
    var mouse = new Guacamole.Mouse(display);

    mouse.onmousedown =
      mouse.onmouseup =
      mouse.onmousemove =
        function (mouseState: any) {
          client.sendMouseState(mouseState);
        };

    // Keyboard
    var keyboard = new Guacamole.Keyboard(document);

    keyboard.onkeydown = function (keysym: any) {
      client.sendKeyEvent(1, keysym);
    };

    keyboard.onkeyup = function (keysym: any) {
      client.sendKeyEvent(0, keysym);
    };

    // connect (store token somewhere safe)
    client.connect(
      "token=hD12AB5Js4WD0Cse6mtgw_8msAieiSi1-vHajL2vAZgAo24yPufxuLKefeZxEYyWXhcbW21iv53Pv18gTXTnXp1i7wClkQ2tDutnIHqrHRo"
    );

    // Disconnect on close
    window.onunload = () => {
      client.disconnect();
    };
  }
}
