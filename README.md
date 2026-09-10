# Jammer
Jammer is a specialized solution for Layer 3 network tunneling, written in C#. It allows you to create an encrypted tunnel between a local computer and a remote exit node to bypass deep packet inspection (DPI) and network restrictions.

## Technical architecture
The project implements the classic TUN-to-TCP tunneling model:
- Incoming traffic (local): captures raw IPv4 datagrams from the virtual network interface (WinTun on Windows)
- Transformation: encapsulates the datagrams in a custom framed transport protocol
- Encryption: applies an ECDH+AES cryptographic layer to encrypt each packet
- Transmission: relays the encrypted data via TCP to the remote exit node (UDP transport is under development)
- Outbound traffic (remote): decapsulates and decrypts traffic on the exit node

---

## Implemented
- L3 tunneling: operates at the IP level, supporting TCP and partially UDP protocols
- WinTun integration: uses the high-performance WinTun driver for Windows
- Packet framing to ensure correct delivery sequencing
- A cryptographic layer using ECDH to establish a shared secret and AES to encrypt data packets
- Split-tunnel routing on the client, so only tunneled traffic goes through the virtual interface while the rest of the connection stays intact

## Planned
- LinuxTun implementation for cross-platform support on Unix-like machines
- A Routing class for Linux routing tables
- IP forwarding + NAT on the exit node, so tunneled traffic can actually reach the public internet
- Traffic obfuscation: adding noise/padding to packet payloads, similar to AmneziaWG

---

# Current architecture

<img width="819" height="1011" alt="image" src="https://github.com/user-attachments/assets/1f8b491c-5cc3-43f9-9759-0a8743a2992a" />


<img width="818" height="1095" alt="image" src="https://github.com/user-attachments/assets/1f890347-f3d0-4c56-ab9f-11a222822553" />

