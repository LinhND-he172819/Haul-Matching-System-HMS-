filepath = 'src/Modules/HMS.Modules.Matching/Application/Services/StaffProposalService.cs'

with open(filepath, 'rb') as f:
    data = f.read()

# Find the garbled message line
garbled = b'Ch? c\xf3 th? duy'
idx = data.find(garbled)
print(f"Garbled text at byte: {idx}")

if idx > 0:
    # Find statement start - the $" before this text
    search_start = max(0, idx - 200)
    # Look for throw new InvalidOperationException near here
    throw_idx = data.rfind(b'throw new InvalidOperationException', search_start, idx)
    print(f"throw at byte: {throw_idx}")
    
    # Find the end of the statement - the ");
    end_idx = data.find(b'");', idx)
    print(f"Statement end at byte: {end_idx}")
    
    if throw_idx > 0 and end_idx > 0:
        old_stmt = data[throw_idx:end_idx+3]
        print(f"Old statement: {old_stmt}")
        
        # New clean English statement
        new_stmt = b'throw new InvalidOperationException("Shipment is in status " + shipment.Status + ". Can only approve when Draft or PendingReview.");'
        
        data = data[:throw_idx] + new_stmt + data[end_idx+3:]
        print(f"New statement: {new_stmt}")
        
        with open(filepath, 'wb') as f:
            f.write(data)
        print(f"\nFile saved ({len(data)} bytes)")
    else:
        print("Could not find throw/end markers")
else:
    print("Garbled text not found - checking if already clean")
    if b'PendingReview' in data:
        lines = data.split(b'\n')
        for i, line in enumerate(lines):
            if b'PendingReview' in line:
                print(f"  Line {i+1}: {line.strip()}")
