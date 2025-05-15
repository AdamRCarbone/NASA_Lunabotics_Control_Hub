import socket

def remap_percentage_to_char(percentage):
    """Map a percentage (0-99) to a character based on joystick axis logic."""
    # Convert percentage (0-99) to joystick axis range (-1 to 1)
    axis_value = (percentage / 99.0) * 2 - 1
    # Apply the original remap logic: (-x + 1) * 62
    char_code = int((-axis_value + 1) * 62)
    return chr(char_code)

def send_controller_packet(left_stick_percentage, right_stick_percentage, hat_position, udp_ip, udp_port):
    """Send a UDP packet with controller data based on input percentages and hat position.
    
    Args:
        left_stick_percentage (int): Left stick position (0-99).
        right_stick_percentage (int): Right stick position (0-99).
        hat_position (int): Hat position (-1, 0, or 1 for up, neutral, down).
        udp_ip (str): Target IP address for the UDP packet.
        udp_port (int): Target port for the UDP packet.
    """
    # Validate inputs
    if not (0 <= left_stick_percentage <= 99):
        raise ValueError("Left stick percentage must be between 0 and 99")
    if not (0 <= right_stick_percentage <= 99):
        raise ValueError("Right stick percentage must be between 0 and 99")
    if hat_position not in [-1, 0, 1]:
        raise ValueError("Hat position must be -1, 0, or 1")
    
    # Create the message: two characters from stick percentages + hat position (shifted to 0-2)
    message = (
        remap_percentage_to_char(left_stick_percentage) +
        remap_percentage_to_char(right_stick_percentage) +
        str(hat_position + 1)
    )
    
    # Send the UDP packet
    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    try:
        sock.sendto(bytes(message, 'ASCII'), (udp_ip, udp_port))
    finally:
        sock.close()