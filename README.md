# Jammer
Jammer is a custom L3 network tunneling solution written in C#. It facilitates an encrypted, obfuscated tunnel between a local machine and a remote exit node to bypass deep packet inspection (DPI) and network restrictions.

Technical Architecture
The project implements a classic TUN-to-UDP tunneling model:
 * Ingress (Local): Captures raw IPv4 datagrams from a virtual network interface (Wintun/TAP)
 * Transformation: Encapsulates datagrams into a custom transport protocol
 * Obfuscation: Applies a cryptographic layer ( ChaCha20 ) to eliminate protocol signatures.
 * Transport: Relays encrypted payloads over UDP to the remote Exit Node
 * Egress (Remote): Decapsulates, decrypts, and forwards traffic to the public internet using NAT
   
---
 
Key Features
 * L3 Tunneling: Operates at the IP layer, supporting TCP, UDP, and ICMP
 * Wintun Integration: Utilizes the high-performance Wintun driver for Windows
 * Signature Masking: Designed to bypass DPI by stripping predictable packet headers and lengths
 * Async Core: Built on .NET 8/9 using System.Net.Sockets with heavy use of ValueTask and Memory<T> for zero-copy efficiency
