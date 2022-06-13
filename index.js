const RTCPeerConnection = window.RTCPeerConnection || window.webkitRTCPeerConnection || window.mozRTCPeerConnection;
const RTCSessionDescription = window.RTCSessionDescription || window.webkitRTCSessionDescription || window.mozRTCSessionDescription;

/* Signaling Utils */
const getSdp = async (roomId, type) => {
  const res = await fetch(`/${roomId}/${type}`, { method: "GET" });
  const sdp = await res.text();
  return (sdp === "") ? null : new RTCSessionDescription({ type, sdp });
};
const postSdp = async (roomId, type, sdp) => {
  await fetch(`/${roomId}/${type}`, {
    method: "POST",
    headers: { "Content-Type": "application/x-www-form-urlencoded" },
    body: encodeURIComponent(sdp)
  });
};
const deleteSdp = async (roomId) => { await postSdp(roomId, "offer", ""); };

/* WebRTC */
const Room = class {
  constructor(localVideo, remoteVideo) {
    this.localVideo = localVideo;
    this.remoteVideo = remoteVideo;
    this.peerConnection = null;
    this.sleep = msec => new Promise(resolve => setTimeout(resolve, msec));
  }

  async create(roomId) {
    await deleteSdp(roomId);
    await this.createConnection(roomId);
    const offer = await this.peerConnection.createOffer();
    await this.peerConnection.setLocalDescription(offer);
    while (this.peerConnection) {
      const answer = await getSdp(roomId, "answer");
      if (answer) {
        await this.peerConnection.setRemoteDescription(answer);
        break;
      }
      await this.sleep(1000);
    }
  }

  async join(roomId) {
    await this.createConnection(roomId);
    while (this.peerConnection) {
      const offer = await getSdp(roomId, "offer");
      if (offer) {
        await this.peerConnection.setRemoteDescription(offer);
        const answer = await this.peerConnection.createAnswer();
        await this.peerConnection.setLocalDescription(answer);
        break;
      }
      await this.sleep(1000);
    }
  }

  leave() {
    if (this.peerConnection) {
      this.peerConnection.close();
      this.peerConnection = null;
    }
    if (this.remoteVideo) {
      this.remoteVideo.pause();
      this.remoteVideo.srcObject = null;
    }
  }

  async createConnection(roomId) {
    this.leave();
    this.peerConnection = new RTCPeerConnection({ iceServers: [] });
    this.peerConnection.ontrack = (event) => {
      this.remoteVideo.srcObject = event.streams[0];
      this.remoteVideo.play();
      this.remoteVideo.volume = 0;
    };
    this.peerConnection.onicecandidate = async (event) => {
      if (event.candidate === null) {
        const desc = this.peerConnection.localDescription;
        await postSdp(roomId, desc.type, desc.sdp);
      }
    };
    this.peerConnection.addStream(this.localVideo.srcObject);
  }
};

document.addEventListener("DOMContentLoaded", () => {
  const localVideo = document.getElementById('local_video');
  const remoteVideo = document.getElementById('remote_video');

  document.getElementById("cameraStart").addEventListener("click", async () => {
    localVideo.srcObject = await navigator.mediaDevices.getUserMedia({ video: true, audio: false });
    localVideo.play();
    localVideo.volume = 0;
  });
  document.getElementById("cameraStop").addEventListener("click", () => {
    localVideo.pause();
    localVideo.srcObject?.getTracks().forEach(track => track.stop());
    localVideo.srcObject = null;
  });

  const room = new Room(localVideo, remoteVideo);
  const getRoomId = () => { return document.getElementById("roomId").value; };
  document.getElementById("roomCreate").addEventListener("click", () => { room.create(getRoomId()); });
  document.getElementById("roomJoin").addEventListener("click", () => { room.join(getRoomId()); });
  document.getElementById("roomLeave").addEventListener("click", () => { room.leave(); });
});
